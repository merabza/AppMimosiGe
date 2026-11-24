using System;
using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 1-ის, „ბრძანებებზე გადასვლის", დამგეგმავი (D194, D196, D200, D202): რომელი ჯგუფი მიიღებს საწყის ბრძანებას
///     (0) და რა თარიღით, რას შეიცავს ეს ბრძანება და რომელ ახალ წესს არღვევს ჯგუფი გადასვლის დღიდან (D0). ჯგუფის
///     არსებულ ვალიდატორებს და დროების გადაფარვას (D178, D179) ბაზის მონაცემი სჭირდება, ამიტომ მათ დარღვევებს ვიზარდის
///     handler-ი უმატებს
/// </summary>
public static class GroupOrdersSwitchPlanner
{
    /// <summary>
    ///     groups: ყველა ჯგუფის ახლანდელი მდგომარეობა ბაზიდან, ჯგუფის Id-ით. ჯგუფების რეჟიმში სტრიქონს ხაზი ჯერ არ აქვს,
    ///     ამიტომ მისი LineId ბაზის სტრიქონის Id-ია (GroupByTeacher.Id, GroupByStudent.GbsId, GroupDayTimePlace.GdtpId).
    ///     transitionDate: გადასვლის დღე (D0). studentName: მოსწავლის სახელი კონტრაქტის Id-ით, დარღვევის აღწერისთვის.
    ///     ჯგუფები რიგდება სასწავლო წლით, კოდით და Id-ით, დარღვევები კი ჯგუფების რიგით და ჯგუფში დღით
    /// </summary>
    public static GroupOrdersSwitchPlan Plan(IReadOnlyDictionary<int, GroupState> groups, DateTime transitionDate,
        Func<int, string> studentName)
    {
        List<GroupOrdersSwitchViolation> violations = [];
        List<GroupOrdersSwitchGroup> planned = [];
        foreach ((int groupId, GroupState state) in groups.OrderBy(g => g.Value.Fields.AcademicYearId)
                     .ThenBy(g => g.Value.Fields.GroupCode, StringComparer.Ordinal).ThenBy(g => g.Key))
        {
            GroupOrderData? initialOrder = InitialOrder(groupId, state, transitionDate);
            planned.Add(new GroupOrdersSwitchGroup(groupId, state.Fields.GroupCode, initialOrder));
            if (initialOrder is null)
            {
                continue;
            }

            //წესები მოწმდება მდგომარეობაზე, რომელსაც საწყისი ბრძანება იძლევა: შემდეგი ბრძანებების შენახვისას ჯგუფი მისგან
            //ითვლება (D194)
            GroupState initialState = GroupOrderEffects.Apply(null, initialOrder).Value;
            violations.AddRange(GroupOrderFormulasRule.Find(initialState, transitionDate)
                .Select(v => FormulaViolation(groupId, v, studentName(v.Row.StudentContractId))));
        }

        return new GroupOrdersSwitchPlan(violations, planned);
    }

    /// <summary>
    ///     საწყისი ბრძანების თარიღი (D196): ჯგუფის პირველი მოქმედი დღე, წარსულში ან მომავალში, თუ ჯგუფი D0-ზე მოქმედია ან
    ///     სტრიქონების თარიღებით მოგვიანებით გახდება მოქმედი; სხვაგვარად null: ასეთი ჯგუფი საწყის ბრძანებას არ იღებს
    /// </summary>
    public static DateTime? InitialOrderDate(GroupState state, DateTime transitionDate)
    {
        IReadOnlyList<GroupActivePeriod> periods = GroupActivity.ActivePeriods(state);
        DateTime d0 = transitionDate.Date;
        //პერიოდი [StartDate, EndDate) D0-ს ან მის შემდეგ დღეს მაშინ შეიცავს, როცა EndDate D0-ზე გვიანია
        return periods.Any(p => p.EndDate is null || p.EndDate > d0) ? periods[0].StartDate : null;
    }

    /// <summary>
    ///     ჯგუფის საწყისი ბრძანება (D196), ან null, თუ ჯგუფი მას არ იღებს (InitialOrderDate). შეიცავს ჯგუფის ველებს,
    ///     გაუქმების თარიღს და ყველა სტრიქონს, დასრულებულებსაც, თავისი თარიღებით. ხაზი სტრიქონს 1:1 იმეორებს და მის
    ///     LineId-ს იღებს: ამ Id-ით უკავშირდება სტრიქონი თავის ხაზს, როცა ხაზი ბაზაში ახალ Id-ს მიიღებს. ბრძანების
    ///     დროებითი Id ჯგუფის Id-ია, რადგან ჯგუფს ერთი საწყისი ბრძანება აქვს
    /// </summary>
    public static GroupOrderData? InitialOrder(int groupId, GroupState state, DateTime transitionDate)
    {
        if (InitialOrderDate(state, transitionDate) is not { } orderDate)
        {
            return null;
        }

        return new GroupOrderData
        {
            Id = groupId,
            OrderType = EGroupOrderType.Initial,
            OrderDate = orderDate,
            GroupId = groupId,
            Fields = state.Fields,
            VoidDate = state.VoidDate,
            Teachers =
            [
                .. state.Teachers.Select(t =>
                    new GroupOrderTeacherLine(t.LineId, t.TeacherContractId, t.SalarySchemaId, t.StartDate, t.EndDate))
            ],
            Students =
            [
                .. state.Students.Select(s => new GroupOrderStudentLine(s.LineId, s.StudentContractId, s.FourWeekHours,
                    s.FourWeekFee, s.OneHourFee, s.HoursCoefficient, s.Note, s.StartDate, s.EndDate))
            ],
            DayTimePlaces =
            [
                .. state.DayTimePlaces.Select(d => new GroupOrderDayTimePlaceLine(d.LineId, d.WeekDayId,
                    d.LessonStartTimeId, d.HoursCount, d.RoomId, d.StartDate, d.EndDate))
            ]
        };
    }

    //ფორმულის დარღვევა (D187, D194): წესი ამ ერთი სტრიქონის შენახვის შეცდომაა, სტრიქონი კი მოსწავლის სახელი
    private static GroupOrdersSwitchViolation FormulaViolation(int groupId, GroupOrderFormulaViolation violation,
        string studentName)
    {
        return new GroupOrdersSwitchViolation(groupId, violation.GroupCode,
            GroupOrderFormulasRule.ToError([violation], v => v.Describe(studentName))!, violation.Date, studentName);
    }
}
