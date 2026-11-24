using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 1-ის შემოწმებები, რომლებიც დამგეგმავის (GroupOrdersSwitchPlanner) ფორმულებს ემატება (D194): ჯგუფის
///     რედაქტორის ვალიდატორები მთელ ისტორიაზე და დროების გადაფარვა სხვა ჯგუფებთან (D178, D179) გადასვლის დღიდან (D212).
///     ორივე მხოლოდ საწყისი ბრძანების მიმღებ ჯგუფებს ამოწმებს, ბაზის მდგომარეობაზე: ბრძანება 0 მას ზუსტად იმეორებს
/// </summary>
public static partial class GroupOrdersSwitchChecks
{
    private const int RegexTimeoutMilliseconds = 1000;

    /// <summary>
    ///     ვალიდატორის შეცდომები დარღვევებად. სტრიქონის შეცდომას (მაგალითად "Students[2].FourWeekFee") სტრიქონის აღწერა და
    ///     მისი დაწყების დღე აქვს: მასწავლებლის ან მოსწავლის სახელი, განრიგის სტრიქონისთვის კვირის დღე და დრო. ჯგუფის
    ///     ველების და სტრიქონების ერთმანეთთან გადაფარვის (D55) შეცდომა მთელ ჯგუფს ეხება. state: ის, რაც ვალიდატორმა
    ///     შეამოწმა (GroupStateMapper.ToGroupRequest სტრიქონების რიგს არ ცვლის)
    /// </summary>
    public static IEnumerable<GroupOrdersSwitchViolation> ValidationViolations(int groupId, GroupState state,
        IEnumerable<GroupValidationFailure> failures, GroupOrdersSwitchNames names)
    {
        return failures.Select(failure => ValidationViolation(groupId, state, failure, names));
    }

    /// <summary>
    ///     დროების გადაფარვა სხვა ჯგუფებთან, როგორც ჯგუფის რედაქტორში (GroupScheduleOverlapsRule, D201), ოღონდ გადასვლის
    ///     დღიდან: მანამდე დასრულებული გადაფარვა არ ბლოკავს (D212). groups: ყველა ჯგუფის მდგომარეობა, checkedGroupIds:
    ///     საწყისი ბრძანების მიმღები ჯგუფები. ყოველი გადაფარვა ცალკე დარღვევაა: წესი ის შეცდომაა, რასაც ჯგუფის შენახვა ამ
    ///     ერთი გადაფარვით დააბრუნებდა, თარიღი მისი პირველი დღე, სტრიქონი მასწავლებლის ან მოსწავლის სახელი. ჯგუფში ჯერ
    ///     მასწავლებლებია, მერე მოსწავლეები
    /// </summary>
    public static IEnumerable<GroupOrdersSwitchViolation> TimesOverlaps(IReadOnlyDictionary<int, GroupState> groups,
        IEnumerable<int> checkedGroupIds, DateTime transitionDate, GroupOrdersSwitchNames names)
    {
        Dictionary<int, GroupSchedule> teacherSchedules = groups.ToDictionary(g => g.Key,
            g => GroupStateMapper.ToTeacherSchedule(g.Key, g.Value, names.LessonStartTimes));
        Dictionary<int, GroupSchedule> studentSchedules = groups.ToDictionary(g => g.Key,
            g => GroupStateMapper.ToStudentSchedule(g.Key, g.Value, names.LessonStartTimes));
        return checkedGroupIds.SelectMany(groupId =>
            GroupScheduleOverlapsRule.Find(teacherSchedules[groupId], teacherSchedules.Values, transitionDate)
                .Select(overlap => OverlapViolation(groupId, groups[groupId], overlap,
                    names.Teachers[overlap.ContractId], GroupErrors.TeacherTimesOverlap, names))
                .Concat(GroupScheduleOverlapsRule
                    .Find(studentSchedules[groupId], studentSchedules.Values, transitionDate)
                    .Select(overlap => OverlapViolation(groupId, groups[groupId], overlap,
                        names.Students[overlap.ContractId], GroupErrors.StudentTimesOverlap, names))));
    }

    /// <summary>
    ///     გეგმა დამატებითი დარღვევებით. რიგი, როგორც დამგეგმავში: ჯგუფების რიგით (სასწავლო წელი, კოდი, Id), ჯგუფში დღით.
    ///     მთელი ჯგუფის დარღვევები ჯგუფში პირველია, ერთი დღისა კი ასე: ვალიდატორები, გადაფარვები, ფორმულები
    /// </summary>
    public static GroupOrdersSwitchPlan AddViolations(GroupOrdersSwitchPlan plan,
        IEnumerable<GroupOrdersSwitchViolation> violations)
    {
        Dictionary<int, int> groupPositions = plan.Groups.Select((group, position) => (group.GroupId, position))
            .ToDictionary(x => x.GroupId, x => x.position);
        return plan with
        {
            Violations =
            [
                .. violations.Concat(plan.Violations).OrderBy(v => groupPositions[v.GroupId])
                    .ThenBy(v => v.Date ?? DateTime.MinValue)
            ]
        };
    }

    private static GroupOrdersSwitchViolation ValidationViolation(int groupId, GroupState state,
        GroupValidationFailure failure, GroupOrdersSwitchNames names)
    {
        string groupCode = state.Fields.GroupCode;
        Match match = RowPropertyRegex().Match(failure.PropertyName);
        if (!match.Success)
        {
            return new GroupOrdersSwitchViolation(groupId, groupCode, failure.Error, null, null);
        }

        int index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
        (DateTime date, string row) = match.Groups["rows"].Value switch
        {
            "Teachers" => (state.Teachers[index].StartDate,
                names.Teachers[state.Teachers[index].TeacherContractId]),
            "Students" => (state.Students[index].StartDate,
                names.Students[state.Students[index].StudentContractId]),
            _ => (state.DayTimePlaces[index].StartDate, ScheduleRow(state.DayTimePlaces[index], names))
        };
        return new GroupOrdersSwitchViolation(groupId, groupCode, failure.Error, date, row);
    }

    //განრიგის სტრიქონი: "ორშაბათი 15:00"
    private static string ScheduleRow(GroupDayTimePlaceRow row, GroupOrdersSwitchNames names)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{names.WeekDays[row.WeekDayId]} {names.LessonStartTimes[row.LessonStartTimeId]:HH:mm}");
    }

    private static GroupOrdersSwitchViolation OverlapViolation(int groupId, GroupState state,
        GroupScheduleOverlap overlap, string memberName, Func<IReadOnlyCollection<string>, Error> error,
        GroupOrdersSwitchNames names)
    {
        return new GroupOrdersSwitchViolation(groupId, state.Fields.GroupCode,
            error([overlap.Describe(memberName, names.WeekDays[overlap.WeekDayId])]), overlap.FromDate, memberName);
    }

    //ვალიდატორის სტრიქონის თვისება: "Teachers[0].StartDate", "Students[2].FourWeekFee", "DayTimePlaces[1].RoomId"
    [GeneratedRegex(@"^(?<rows>Teachers|Students|DayTimePlaces)\[(?<index>\d+)\]", RegexOptions.None,
        RegexTimeoutMilliseconds)]
    private static partial Regex RowPropertyRegex();
}
