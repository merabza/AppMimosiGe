using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Groups.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupScheduleOverlapsRuleTests
{
    private const int Monday = 1;
    private const int Wednesday = 3;

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //a teacher or student row: the rule is the same for both (D178, D179)
    private static GroupScheduleMember Member(int contractId = 5, DateTime? startDate = null, DateTime? endDate = null) =>
        new(contractId, startDate ?? Day(9, 1), endDate);

    private static GroupScheduleDayTime DayTime(int hour, int minute = 0, float hoursCount = 1.5f,
        int weekDayId = Monday, DateTime? startDate = null, DateTime? endDate = null) =>
        new(weekDayId, new TimeOnly(hour, minute), hoursCount, startDate ?? Day(9, 1), endDate);

    private static GroupSchedule Group(int grpId, string groupCode, GroupScheduleMember[] members,
        GroupScheduleDayTime[] dayTimes, DateTime? voidDate = null) =>
        new(grpId, groupCode, voidDate, members, dayTimes);

    //the saved group 42: contract 5 since 01.09, Monday 15:00–16:30 since 01.09
    private static GroupSchedule Edited(DateTime? voidDate = null) =>
        Group(42, "1001", [Member()], [DayTime(15)], voidDate);

    //today is 01.08, before every day of the groups, so no overlap is over yet
    private static IReadOnlyList<GroupScheduleOverlap> Find(GroupSchedule schedule,
        params GroupSchedule[] otherSchedules) =>
        FindOn(Day(8, 1), schedule, otherSchedules);

    private static IReadOnlyList<GroupScheduleOverlap> FindOn(DateTime today, GroupSchedule schedule,
        params GroupSchedule[] otherSchedules) =>
        GroupScheduleOverlapsRule.Find(schedule, otherSchedules, today);

    //the days are those when both groups have the member and the Monday row: 15.09 until 05.10
    [Fact]
    public void Find_SameMemberAndWeekDayAtIntersectingTimes_IsAnOverlapForTheCommonDays()
    {
        IReadOnlyList<GroupScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Member(startDate: Day(9, 15), endDate: Day(10, 5))], [DayTime(16)]));

        Assert.Equal(
        [
            new GroupScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(16, 0), new TimeOnly(17, 30), Day(9, 15),
                Day(10, 5))
        ], overlaps);
    }

    //D203: the common days are 15.09 until 05.10, so the last one is 04.10. An overlap that is over does not count
    [Theory]
    [InlineData(9, 1, true)]
    [InlineData(9, 20, true)]
    [InlineData(10, 4, true)]
    [InlineData(10, 5, false)]
    [InlineData(11, 20, false)]
    public void Find_OverlapCountsUntilItsLastDay(int todayMonth, int todayDay, bool counts)
    {
        IReadOnlyList<GroupScheduleOverlap> overlaps = FindOn(Day(todayMonth, todayDay), Edited(),
            Group(7, "1002", [Member(startDate: Day(9, 15), endDate: Day(10, 5))], [DayTime(16)]));

        Assert.Equal(counts, overlaps.Count == 1);
    }

    //D203: an overlap that goes on after today keeps its first day, which is before today
    [Fact]
    public void Find_OverlapThatGoesOnAfterToday_HasAllItsDays()
    {
        GroupScheduleOverlap overlap = Assert.Single(FindOn(Day(11, 20), Edited(),
            Group(7, "1002", [Member(startDate: Day(10, 1))], [DayTime(14, 30)])));

        Assert.Equal(Day(10, 1), overlap.FromDate);
        Assert.Null(overlap.ToDate);
    }

    //D203: of the two Monday rows of the other group only the one at the same time ended before today (20.11)
    [Fact]
    public void Find_OnlyTheOverlapsThatAreOver_DoNotCount()
    {
        GroupSchedule other = Group(7, "1002", [Member()],
            [DayTime(15, endDate: Day(10, 5)), DayTime(16, startDate: Day(10, 5), endDate: Day(11, 21))]);

        GroupScheduleOverlap overlap = Assert.Single(FindOn(Day(11, 20), Edited(), other));

        Assert.Equal(new TimeOnly(16, 0), overlap.OtherStartTime);
        Assert.Equal(Day(11, 21), overlap.ToDate);
    }

    [Fact]
    public void Find_OpenPeriodsOnBothSides_HaveNoEnd()
    {
        GroupScheduleOverlap overlap = Assert.Single(Find(Edited(),
            Group(7, "1002", [Member(startDate: Day(10, 1))], [DayTime(14, 30)])));

        Assert.Equal(Day(10, 1), overlap.FromDate);
        Assert.Null(overlap.ToDate);
    }

    // [start, start + hours): a lesson that ends when the other starts is no overlap
    [Theory]
    [InlineData(13, 30, 1.5f, false)]
    [InlineData(13, 30, 2f, true)]
    [InlineData(15, 0, 0.5f, true)]
    [InlineData(16, 0, 1f, true)]
    [InlineData(16, 30, 1f, false)]
    [InlineData(9, 0, 8f, true)]
    public void Find_TimesOfTheOtherGroup(int hour, int minute, float hoursCount, bool overlap)
    {
        IReadOnlyList<GroupScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Member()], [DayTime(hour, minute, hoursCount)]));

        Assert.Equal(overlap, overlaps.Count == 1);
    }

    [Fact]
    public void Find_OtherWeekDayOrOtherContract_IsNoOverlap()
    {
        Assert.Empty(Find(Edited(), Group(7, "1002", [Member()], [DayTime(15, weekDayId: Wednesday)]),
            Group(8, "1003", [Member(6)], [DayTime(15)])));
    }

    // [StartDate, EndDate): the other group's row ends on 01.09, the day the edited group starts
    [Fact]
    public void Find_PeriodsThatOnlyTouch_AreNoOverlap()
    {
        Assert.Empty(Find(Edited(),
            Group(7, "1002", [Member(startDate: Day(8, 1), endDate: Day(9, 1))],
                [DayTime(15, startDate: Day(8, 1))])));
    }

    //the member row of the edited group ends on 20.09, its Monday row runs from 10.09 until 25.09
    [Fact]
    public void Find_TheMemberAndTheScheduleRowMustBothBeActive()
    {
        GroupSchedule edited = Group(42, "1001", [Member(endDate: Day(9, 20))],
            [DayTime(15, startDate: Day(9, 10), endDate: Day(9, 25))]);

        GroupScheduleOverlap overlap = Assert.Single(Find(edited, Group(7, "1002", [Member()], [DayTime(15)])));

        Assert.Equal(Day(9, 10), overlap.FromDate);
        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //the member had left before the Monday row started, so the member never has this lesson
    [Fact]
    public void Find_MemberRowEndedBeforeTheScheduleRow_IsNoOverlap()
    {
        GroupSchedule edited = Group(42, "1001", [Member(endDate: Day(9, 10))], [DayTime(15, startDate: Day(9, 10))]);

        Assert.Empty(Find(edited, Group(7, "1002", [Member()], [DayTime(15)])));
    }

    [Fact]
    public void Find_TheOtherGroupsVoidDateEndsItsLessons()
    {
        GroupScheduleOverlap overlap =
            Assert.Single(Find(Edited(), Group(7, "1002", [Member()], [DayTime(15)], Day(9, 20))));

        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //the edited group's member stays until 05.10, the other group is voided on 20.09
    [Fact]
    public void Find_OverlapEndsWhenTheFirstOfTheTwoLessonsEnds()
    {
        GroupScheduleOverlap overlap = Assert.Single(Find(
            Group(42, "1001", [Member(endDate: Day(10, 5))], [DayTime(15)]),
            Group(7, "1002", [Member()], [DayTime(15)], Day(9, 20))));

        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //old data: the other group has two Monday rows at once, listed late time first
    [Fact]
    public void Find_TwoLessonsOfTheOtherGroupOnOneDay_AreInTheOrderOfTheirTime()
    {
        IReadOnlyList<GroupScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Member()], [DayTime(15, 30), DayTime(15)]));

        Assert.Equal([new TimeOnly(15, 0), new TimeOnly(15, 30)], overlaps.Select(o => o.OtherStartTime));
    }

    [Fact]
    public void Find_TheEditedGroupsVoidDateEndsItsLessons()
    {
        Assert.Equal(Day(9, 20),
            Assert.Single(Find(Edited(Day(9, 20)), Group(7, "1002", [Member()], [DayTime(15)]))).ToDate);
        Assert.Empty(Find(Edited(Day(9, 1)), Group(7, "1002", [Member()], [DayTime(15)])));
    }

    //the saved version of the edited group is never compared with its new version
    [Fact]
    public void Find_GroupWithTheSameId_IsNotCompared()
    {
        Assert.Empty(Find(Edited(), Group(42, "1001", [Member()], [DayTime(15)])));
    }

    [Fact]
    public void Find_GroupWithoutMembersOrSchedule_HasNoOverlaps()
    {
        Assert.Empty(Find(Group(42, "1001", [], [DayTime(15)]), Group(7, "1002", [Member()], [DayTime(15)])));
        Assert.Empty(Find(Group(42, "1001", [Member()], []), Group(7, "1002", [Member()], [DayTime(15)])));
    }

    //every member row of the edited group: contract 5 until 01.10, then contract 6 on Mondays and Wednesdays
    [Fact]
    public void Find_SeveralOverlaps_AreInTheOrderOfDaysGroupsWeekDaysTimesAndContracts()
    {
        GroupSchedule edited = Group(42, "1001", [Member(endDate: Day(10, 1)), Member(6, Day(10, 1))],
            [DayTime(15), DayTime(15, weekDayId: Wednesday)]);

        (int, string, int, DateTime, DateTime?)[] expected =
        [
            (5, "1003", Monday, Day(9, 1), Day(10, 1)),
            (5, "1010", Wednesday, Day(9, 15), Day(10, 1)),
            (6, "1003", Monday, Day(10, 1), null),
            (6, "1009", Monday, Day(10, 1), null),
            (6, "1009", Wednesday, Day(10, 1), null)
        ];

        IReadOnlyList<GroupScheduleOverlap> overlaps = Find(edited,
            Group(9, "1009", [Member(6)], [DayTime(16, weekDayId: Wednesday), DayTime(14, 30)]),
            Group(8, "1003", [Member(), Member(6, Day(10, 1))], [DayTime(15, 30)]),
            Group(7, "1010", [Member(5, Day(9, 15))], [DayTime(15, weekDayId: Wednesday)]));

        Assert.Equal(expected,
            overlaps.Select(o => (o.ContractId, o.OtherGroupCode, o.WeekDayId, o.FromDate, o.ToDate)));
    }

    //D179: students 21 and 20 of the edited group both learn in group 7 at that time, on the same days
    [Fact]
    public void Find_SeveralStudentsInTheSameOtherLesson_AreInTheOrderOfTheirContracts()
    {
        GroupSchedule edited = Group(42, "1001", [Member(21), Member(20)], [DayTime(15)]);

        IReadOnlyList<GroupScheduleOverlap> overlaps =
            Find(edited, Group(7, "1002", [Member(21), Member(20)], [DayTime(15, 30)]));

        Assert.Equal([20, 21], overlaps.Select(o => o.ContractId));
    }

    [Fact]
    public void Describe_ClosedPeriod_ShowsTheMemberTheGroupTheTimeAndTheDays()
    {
        var overlap = new GroupScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(9, 30), new TimeOnly(11, 0),
            Day(9, 15), Day(10, 5));

        Assert.Equal("ალფა ანა / T5 — ჯგუფი 1002, ორშაბათი 09:30–11:00, 15.09.2026–05.10.2026",
            overlap.Describe("ალფა ანა / T5", "ორშაბათი"));
    }

    [Fact]
    public void Describe_OpenPeriod_ShowsItsFirstDay()
    {
        var overlap = new GroupScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(15, 0), new TimeOnly(16, 30),
            Day(9, 15), null);

        Assert.Equal("T — ჯგუფი 1002, W 15:00–16:30, 15.09.2026-დან", overlap.Describe("T", "W"));
    }

    //the end of a lesson that passes midnight is shown as the time of the next day
    [Fact]
    public void DayTime_EndIsStartPlusHours()
    {
        GroupScheduleDayTime dayTime = DayTime(23, 0, 2f);

        Assert.Equal(TimeSpan.FromHours(25), dayTime.End);
        Assert.Equal(new TimeOnly(1, 0), dayTime.EndTime);
    }
}
