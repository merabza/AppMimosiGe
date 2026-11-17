using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Groups.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class TeacherScheduleOverlapsRuleTests
{
    private const int Monday = 1;
    private const int Wednesday = 3;

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static TeacherScheduleTeacher Teacher(int teacherContractId = 5, DateTime? startDate = null,
        DateTime? endDate = null) =>
        new(teacherContractId, startDate ?? Day(9, 1), endDate);

    private static TeacherScheduleDayTime DayTime(int hour, int minute = 0, float hoursCount = 1.5f,
        int weekDayId = Monday, DateTime? startDate = null, DateTime? endDate = null) =>
        new(weekDayId, new TimeOnly(hour, minute), hoursCount, startDate ?? Day(9, 1), endDate);

    private static TeacherScheduleGroup Group(int grpId, string groupCode, TeacherScheduleTeacher[] teachers,
        TeacherScheduleDayTime[] dayTimes, DateTime? voidDate = null) =>
        new(grpId, groupCode, voidDate, teachers, dayTimes);

    //the saved group 42: teacher contract 5 since 01.09, Monday 15:00–16:30 since 01.09
    private static TeacherScheduleGroup Edited(DateTime? voidDate = null) =>
        Group(42, "1001", [Teacher()], [DayTime(15)], voidDate);

    private static IReadOnlyList<TeacherScheduleOverlap> Find(TeacherScheduleGroup scheduleGroup,
        params TeacherScheduleGroup[] otherGroups) =>
        TeacherScheduleOverlapsRule.Find(scheduleGroup, otherGroups);

    //the days are those when both groups have the teacher and the Monday row: 15.09 until 05.10
    [Fact]
    public void Find_SameTeacherAndWeekDayAtIntersectingTimes_IsAnOverlapForTheCommonDays()
    {
        IReadOnlyList<TeacherScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Teacher(startDate: Day(9, 15), endDate: Day(10, 5))], [DayTime(16)]));

        Assert.Equal(
        [
            new TeacherScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(16, 0), new TimeOnly(17, 30), Day(9, 15),
                Day(10, 5))
        ], overlaps);
    }

    [Fact]
    public void Find_OpenPeriodsOnBothSides_HaveNoEnd()
    {
        TeacherScheduleOverlap overlap = Assert.Single(Find(Edited(),
            Group(7, "1002", [Teacher(startDate: Day(10, 1))], [DayTime(14, 30)])));

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
        IReadOnlyList<TeacherScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Teacher()], [DayTime(hour, minute, hoursCount)]));

        Assert.Equal(overlap, overlaps.Count == 1);
    }

    [Fact]
    public void Find_OtherWeekDayOrOtherTeacher_IsNoOverlap()
    {
        Assert.Empty(Find(Edited(), Group(7, "1002", [Teacher()], [DayTime(15, weekDayId: Wednesday)]),
            Group(8, "1003", [Teacher(6)], [DayTime(15)])));
    }

    // [StartDate, EndDate): the other group's teacher leaves on 01.09, the day the edited group starts
    [Fact]
    public void Find_PeriodsThatOnlyTouch_AreNoOverlap()
    {
        Assert.Empty(Find(Edited(),
            Group(7, "1002", [Teacher(startDate: Day(8, 1), endDate: Day(9, 1))],
                [DayTime(15, startDate: Day(8, 1))])));
    }

    //the teacher row of the edited group ends on 20.09, its Monday row runs from 10.09 until 25.09
    [Fact]
    public void Find_TheTeacherAndTheScheduleRowMustBothBeActive()
    {
        TeacherScheduleGroup edited = Group(42, "1001", [Teacher(endDate: Day(9, 20))],
            [DayTime(15, startDate: Day(9, 10), endDate: Day(9, 25))]);

        TeacherScheduleOverlap overlap = Assert.Single(Find(edited, Group(7, "1002", [Teacher()], [DayTime(15)])));

        Assert.Equal(Day(9, 10), overlap.FromDate);
        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //the teacher had left before the Monday row started, so the teacher never has this lesson
    [Fact]
    public void Find_TeacherRowEndedBeforeTheScheduleRow_IsNoOverlap()
    {
        TeacherScheduleGroup edited = Group(42, "1001", [Teacher(endDate: Day(9, 10))],
            [DayTime(15, startDate: Day(9, 10))]);

        Assert.Empty(Find(edited, Group(7, "1002", [Teacher()], [DayTime(15)])));
    }

    [Fact]
    public void Find_TheOtherGroupsVoidDateEndsItsLessons()
    {
        TeacherScheduleOverlap overlap =
            Assert.Single(Find(Edited(), Group(7, "1002", [Teacher()], [DayTime(15)], Day(9, 20))));

        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //the edited group's teacher stays until 05.10, the other group is voided on 20.09
    [Fact]
    public void Find_OverlapEndsWhenTheFirstOfTheTwoLessonsEnds()
    {
        TeacherScheduleOverlap overlap = Assert.Single(Find(
            Group(42, "1001", [Teacher(endDate: Day(10, 5))], [DayTime(15)]),
            Group(7, "1002", [Teacher()], [DayTime(15)], Day(9, 20))));

        Assert.Equal(Day(9, 20), overlap.ToDate);
    }

    //old data: the other group has two Monday rows at once, listed late time first
    [Fact]
    public void Find_TwoLessonsOfTheOtherGroupOnOneDay_AreInTheOrderOfTheirTime()
    {
        IReadOnlyList<TeacherScheduleOverlap> overlaps = Find(Edited(),
            Group(7, "1002", [Teacher()], [DayTime(15, 30), DayTime(15)]));

        Assert.Equal([new TimeOnly(15, 0), new TimeOnly(15, 30)], overlaps.Select(o => o.OtherStartTime));
    }

    [Fact]
    public void Find_TheEditedGroupsVoidDateEndsItsLessons()
    {
        Assert.Equal(Day(9, 20),
            Assert.Single(Find(Edited(Day(9, 20)), Group(7, "1002", [Teacher()], [DayTime(15)]))).ToDate);
        Assert.Empty(Find(Edited(Day(9, 1)), Group(7, "1002", [Teacher()], [DayTime(15)])));
    }

    //the students are not checked: a group without students still takes the teacher's time
    [Fact]
    public void Find_OtherGroupWithoutStudents_StillOverlaps()
    {
        Assert.Single(Find(Edited(), Group(7, "1002", [Teacher()], [DayTime(15)])));
    }

    //the saved version of the edited group is never compared with its new version
    [Fact]
    public void Find_GroupWithTheSameId_IsNotCompared()
    {
        Assert.Empty(Find(Edited(), Group(42, "1001", [Teacher()], [DayTime(15)])));
    }

    [Fact]
    public void Find_GroupWithoutTeachersOrSchedule_HasNoOverlaps()
    {
        Assert.Empty(Find(Group(42, "1001", [], [DayTime(15)]), Group(7, "1002", [Teacher()], [DayTime(15)])));
        Assert.Empty(Find(Group(42, "1001", [Teacher()], []), Group(7, "1002", [Teacher()], [DayTime(15)])));
    }

    //every teacher row of the edited group: contract 5 until 01.10, then contract 6 on Mondays and Wednesdays
    [Fact]
    public void Find_SeveralOverlaps_AreInTheOrderOfDaysGroupsWeekDaysTimesAndTeachers()
    {
        TeacherScheduleGroup edited = Group(42, "1001", [Teacher(endDate: Day(10, 1)), Teacher(6, Day(10, 1))],
            [DayTime(15), DayTime(15, weekDayId: Wednesday)]);

        (int, string, int, DateTime, DateTime?)[] expected =
        [
            (5, "1003", Monday, Day(9, 1), Day(10, 1)),
            (5, "1010", Wednesday, Day(9, 15), Day(10, 1)),
            (6, "1003", Monday, Day(10, 1), null),
            (6, "1009", Monday, Day(10, 1), null),
            (6, "1009", Wednesday, Day(10, 1), null)
        ];

        IReadOnlyList<TeacherScheduleOverlap> overlaps = Find(edited,
            Group(9, "1009", [Teacher(6)], [DayTime(16, weekDayId: Wednesday), DayTime(14, 30)]),
            Group(8, "1003", [Teacher(), Teacher(6, Day(10, 1))], [DayTime(15, 30)]),
            Group(7, "1010", [Teacher(5, Day(9, 15))], [DayTime(15, weekDayId: Wednesday)]));

        Assert.Equal(expected,
            overlaps.Select(o => (o.TeacherContractId, o.OtherGroupCode, o.WeekDayId, o.FromDate, o.ToDate)));
    }

    [Fact]
    public void Describe_ClosedPeriod_ShowsTheTeacherTheGroupTheTimeAndTheDays()
    {
        var overlap = new TeacherScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(9, 30), new TimeOnly(11, 0),
            Day(9, 15), Day(10, 5));

        Assert.Equal("ალფა ანა / T5 — ჯგუფი 1002, ორშაბათი 09:30–11:00, 15.09.2026–05.10.2026",
            overlap.Describe("ალფა ანა / T5", "ორშაბათი"));
    }

    [Fact]
    public void Describe_OpenPeriod_ShowsItsFirstDay()
    {
        var overlap = new TeacherScheduleOverlap(5, 7, "1002", Monday, new TimeOnly(15, 0), new TimeOnly(16, 30),
            Day(9, 15), null);

        Assert.Equal("T — ჯგუფი 1002, W 15:00–16:30, 15.09.2026-დან", overlap.Describe("T", "W"));
    }

    //the end of a lesson that passes midnight is shown as the time of the next day
    [Fact]
    public void DayTime_EndIsStartPlusHours()
    {
        TeacherScheduleDayTime dayTime = DayTime(23, 0, 2f);

        Assert.Equal(TimeSpan.FromHours(25), dayTime.End);
        Assert.Equal(new TimeOnly(1, 0), dayTime.EndTime);
    }
}
