using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.Tests.Groups;
using AppMimosiGeShared.Contracts.Errors;
using FluentValidation.Results;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდი 1-ის შემოწმებები დამგეგმავის ფორმულების გარდა (D194): ჯგუფის რედაქტორის ვალიდატორები მთელ ისტორიაზე და
//დროების გადაფარვა გადასვლის დღიდან (D178, D179, D201, D212)
public sealed class GroupOrdersSwitchChecksTests
{
    //გადასვლის დღე (D0)
    private static readonly DateTime D0 = Day(10, 1);

    private static readonly GroupOrdersSwitchNames Names = new(
        new Dictionary<int, string> { [5] = "მასწავლებელი 5", [6] = "მასწავლებელი 6" },
        new Dictionary<int, string> { [20] = "მოსწავლე 20", [21] = "მოსწავლე 21" },
        new Dictionary<int, string> { [1] = "ორშაბათი", [3] = "ოთხშაბათი" },
        new Dictionary<int, TimeOnly> { [4] = new(15, 0), [5] = new(16, 0) });

    //ჯგუფი კოდით: მასწავლებელი, მოსწავლე და ორშაბათის 15:00–17:00 01.09-იდან
    private static GroupState Group(string groupCode, int teacherContractId = 5, int studentContractId = 20,
        DateTime? teacherEndDate = null, GroupStudentRow[]? students = null)
    {
        GroupStudentRow student = new(2, studentContractId, 8f, 48m, 6m, 1f, null, Day(9, 1), null);
        return State(teachers: [new GroupTeacherRow(1, teacherContractId, 8, Day(9, 1), teacherEndDate)],
            students: students ?? [student]) with
        {
            Fields = Fields with { GroupCode = groupCode }
        };
    }

    private static List<GroupOrdersSwitchViolation> Overlaps(Dictionary<int, GroupState> groups,
        params int[] checkedGroupIds)
    {
        return [.. GroupOrdersSwitchChecks.TimesOverlaps(groups, checkedGroupIds, D0, Names)];
    }

    private static async Task<List<GroupOrdersSwitchViolation>> Validate(GroupState state)
    {
        var validator = new GroupRequestValidator(GroupTestData.RepositoryWhereEverythingExists().Object,
            GroupTestData.StudentContractsRepositoryWhereEverythingExists().Object,
            GroupTestData.TeacherContractsRepositoryWhereEverythingExists().Object);
        ValidationResult result = await validator.ValidateAsync(GroupStateMapper.ToGroupRequest(state));
        return
        [
            .. GroupOrdersSwitchChecks.ValidationViolations(42, state,
                result.Errors.Select(f =>
                    new GroupValidationFailure(f.PropertyName, Error.Problem(f.ErrorCode, f.ErrorMessage))), Names)
        ];
    }

    //ნამდვილი ვალიდატორის თვისებები ("Students[1].FourWeekFee"): სტრიქონის შეცდომას მისი სახელი და დაწყება აქვს
    [Fact]
    public async Task ValidationViolations_RowErrorsOfTheRealValidator_NameTheRowAndItsStart()
    {
        GroupState state = State(
            teachers: [new GroupTeacherRow(1, 5, 8, Day(9, 2), Day(9, 2))],
            students:
            [
                new GroupStudentRow(2, 20, 8f, 48m, 6m, 1f, null, Day(9, 1), null),
                new GroupStudentRow(3, 21, 8f, 0m, 6m, 1f, null, Day(9, 3), null)
            ],
            dayTimePlaces: [new GroupDayTimePlaceRow(4, 1, 4, 0f, 1, Day(9, 4), null)]);

        List<GroupOrdersSwitchViolation> violations = await Validate(state);

        Assert.Equal(
        [
            (GroupErrors.EndDateMustBeAfterStartDate.Code, (DateTime?)Day(9, 2), "მასწავლებელი 5"),
            (GroupErrors.FeeMustBePositive.Code, Day(9, 3), "მოსწავლე 21"),
            (GroupErrors.HoursCountMustBePositive.Code, Day(9, 4), "ორშაბათი 15:00")
        ], violations.Select(v => (v.Rule.Code, v.Date, v.Row)));
        Assert.All(violations, v => Assert.Equal((42, "601"), (v.GroupId, v.GroupCode)));
        Assert.Equal(GroupErrors.FeeMustBePositive.Description, violations[1].Rule.Description);
    }

    //D55: სტრიქონების ერთმანეთთან გადაფარვა მთელ ჯგუფს ეხება: თარიღისა და სტრიქონის გარეშე
    [Fact]
    public async Task ValidationViolations_OverlappingRowsOfTheGroup_ConcernTheWholeGroup()
    {
        GroupState state = State(teachers:
        [
            new GroupTeacherRow(1, 5, 8, Day(9, 1), null), new GroupTeacherRow(2, 6, 8, Day(9, 15), null)
        ]);

        GroupOrdersSwitchViolation violation = Assert.Single(await Validate(state));

        Assert.Equal((GroupErrors.TeacherPeriodsOverlap.Code, (DateTime?)null, (string?)null),
            (violation.Rule.Code, violation.Date, violation.Row));
    }

    [Fact]
    public async Task ValidationViolations_ValidGroup_HasNone()
    {
        Assert.Empty(await Validate(State()));
    }

    //ჯგუფის ველის შეცდომა მთელ ჯგუფს ეხება; უცნობი სტრიქონის თვისებაც
    [Theory]
    [InlineData("GroupCode")]
    [InlineData("Students")]
    [InlineData("Rooms[0].RoomId")]
    public void ValidationViolations_NotARowProperty_ConcernsTheWholeGroup(string propertyName)
    {
        GroupOrdersSwitchViolation violation = Assert.Single(GroupOrdersSwitchChecks.ValidationViolations(7, State(),
            [new GroupValidationFailure(propertyName, GroupErrors.GroupCodeIsRequired)], Names));

        Assert.Equal((7, "601", GroupErrors.GroupCodeIsRequired, (DateTime?)null, (string?)null),
            (violation.GroupId, violation.GroupCode, violation.Rule, violation.Date, violation.Row));
    }

    //D178, D212: მასწავლებლის გადაფარვა, რომელიც D0-ზე გრძელდება, ბლოკავს; წესი იგივე შეცდომაა, რასაც ჯგუფის
    //შენახვა დააბრუნებდა ამ გადაფარვით
    [Fact]
    public void TimesOverlaps_TeacherOverlapGoingOnAtD0_IsAViolationOfTheCheckedGroup()
    {
        Dictionary<int, GroupState> groups = new()
        {
            [1] = Group("601"), [2] = Group("602", studentContractId: 21)
        };

        GroupOrdersSwitchViolation violation = Assert.Single(Overlaps(groups, 1));

        Assert.Equal((1, "601", (DateTime?)Day(9, 1), "მასწავლებელი 5"),
            (violation.GroupId, violation.GroupCode, violation.Date, violation.Row));
        Assert.Equal(
            GroupErrors.TeacherTimesOverlap(["მასწავლებელი 5 — ჯგუფი 602, ორშაბათი 15:00–17:00, 01.09.2026-დან"]),
            violation.Rule);
    }

    //D212: D0-მდე დასრულებული გადაფარვა არ ბლოკავს, დღეს გაგრძელებული კი ბლოკავს
    [Theory]
    [InlineData(10, 1, false)]
    [InlineData(10, 2, true)]
    public void TimesOverlaps_OverlapEndingAroundD0_BlocksOnlyIfItReachesD0(int endMonth, int endDay, bool blocks)
    {
        Dictionary<int, GroupState> groups = new()
        {
            [1] = Group("601"),
            [2] = Group("602", studentContractId: 21, teacherEndDate: Day(endMonth, endDay))
        };

        Assert.Equal(blocks, Overlaps(groups, 1).Count == 1);
    }

    //D179: მოსწავლის გადაფარვა მოსწავლის სახელით
    [Fact]
    public void TimesOverlaps_StudentInTwoGroupsAtTheSameTime_IsAStudentViolation()
    {
        Dictionary<int, GroupState> groups = new() { [1] = Group("601"), [2] = Group("602", 6) };

        GroupOrdersSwitchViolation violation = Assert.Single(Overlaps(groups, 1));

        Assert.Equal((nameof(GroupErrors.StudentTimesOverlap), (DateTime?)Day(9, 1), "მოსწავლე 20"),
            (violation.Rule.Code, violation.Date, violation.Row));
        Assert.Contains("მოსწავლე 20 — ჯგუფი 602, ორშაბათი 15:00–17:00, 01.09.2026-დან",
            violation.Rule.Description, StringComparison.Ordinal);
    }

    //D201: ჯგუფი, რომელსაც მოსწავლე არ ჰყავს, მასწავლებლის დროს არ იკავებს
    [Fact]
    public void TimesOverlaps_OtherGroupWithoutStudents_TakesNoTime()
    {
        Dictionary<int, GroupState> groups = new() { [1] = Group("601"), [2] = Group("602", students: []) };

        Assert.Empty(Overlaps(groups, 1));
    }

    //ორივე ჯგუფის შემოწმებისას გადაფარვა თითოეულის დარღვევაა; ჯგუფში ჯერ მასწავლებლები, მერე მოსწავლეები.
    //შეუმოწმებელ ჯგუფს დარღვევა არ აქვს
    [Fact]
    public void TimesOverlaps_CheckedGroups_HaveTheirOwnViolationsTeachersFirst()
    {
        Dictionary<int, GroupState> groups = new() { [1] = Group("601"), [2] = Group("602"), [3] = Group("603") };

        List<GroupOrdersSwitchViolation> violations = Overlaps(groups, 2, 1);

        Assert.Equal(
        [
            (2, nameof(GroupErrors.TeacherTimesOverlap)), (2, nameof(GroupErrors.TeacherTimesOverlap)),
            (2, nameof(GroupErrors.StudentTimesOverlap)), (2, nameof(GroupErrors.StudentTimesOverlap)),
            (1, nameof(GroupErrors.TeacherTimesOverlap)), (1, nameof(GroupErrors.TeacherTimesOverlap)),
            (1, nameof(GroupErrors.StudentTimesOverlap)), (1, nameof(GroupErrors.StudentTimesOverlap))
        ], violations.Select(v => (v.GroupId, v.Rule.Code)));
    }

    //რიგი დამგეგმავისაა: ჯგუფების რიგით, ჯგუფში დღით, მთელი ჯგუფის დარღვევა პირველია; ერთი დღისა დამატების
    //რიგით, ფორმულები ბოლოს
    [Fact]
    public void AddViolations_MergesByGroupOrderThenDay()
    {
        GroupOrdersSwitchViolation formula = Violation(2, GroupErrors.TeacherPeriodsOverlap, Day(10, 1));
        var plan = new GroupOrdersSwitchPlan([formula],
            [new GroupOrdersSwitchGroup(2, "602", null), new GroupOrdersSwitchGroup(1, "701", null)]);
        GroupOrdersSwitchViolation wholeGroup = Violation(1, GroupErrors.GroupCodeIsRequired, null);
        GroupOrdersSwitchViolation firstGroupOverlap = Violation(2, GroupErrors.RoomNotFound, Day(10, 1));
        GroupOrdersSwitchViolation earlier = Violation(2, GroupErrors.WeekDayNotFound, Day(9, 5));
        GroupOrdersSwitchViolation secondGroupDay = Violation(1, GroupErrors.RoomNotFound, Day(9, 1));

        GroupOrdersSwitchPlan merged = GroupOrdersSwitchChecks.AddViolations(plan,
            [secondGroupDay, firstGroupOverlap, wholeGroup, earlier]);

        Assert.Equal([earlier, firstGroupOverlap, formula, wholeGroup, secondGroupDay], merged.Violations);
        Assert.Equal(plan.Groups, merged.Groups);
    }

    private static GroupOrdersSwitchViolation Violation(int groupId, Error rule, DateTime? date)
    {
        return new GroupOrdersSwitchViolation(groupId, "g", rule, date, null);
    }
}
