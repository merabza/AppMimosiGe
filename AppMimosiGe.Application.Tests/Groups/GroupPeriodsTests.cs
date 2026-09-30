using System;
using AppMimosiGe.Application.Groups.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupPeriodsTests
{
    private static DateTime Day(int day) => new(2026, 9, day, 0, 0, 0, DateTimeKind.Unspecified);

    [Theory]
    //[1, 10) and [5, 20)
    [InlineData(1, 10, 5, 20, true)]
    //[1, 10) and [10, 20): the end day is not in the period any more
    [InlineData(1, 10, 10, 20, false)]
    //[10, 20) and [1, 10)
    [InlineData(10, 20, 1, 10, false)]
    //[1, 30) contains [5, 6)
    [InlineData(1, 30, 5, 6, true)]
    //[1, ∞) and [20, 25)
    [InlineData(1, null, 20, 25, true)]
    //[1, 10) and [10, ∞)
    [InlineData(1, 10, 10, null, false)]
    //[1, ∞) and [2, ∞)
    [InlineData(1, null, 2, null, true)]
    //[5, 6) and [5, 6)
    [InlineData(5, 6, 5, 6, true)]
    public void Overlap_UsesHalfOpenPeriods(int start1, int? end1, int start2, int? end2, bool expected)
    {
        Assert.Equal(expected, GroupPeriods.Overlap(Day(start1), end1 is null ? null : Day(end1.Value), Day(start2),
            end2 is null ? null : Day(end2.Value)));
    }

    // only the days count, not the time of day
    [Fact]
    public void Overlap_IgnoresTimeOfDay()
    {
        Assert.False(GroupPeriods.Overlap(Day(1), Day(10).AddHours(12), Day(10), null));
    }

    [Fact]
    public void AnyTeacherPeriodsOverlap_ChecksEveryPair()
    {
        Assert.False(GroupPeriods.AnyTeacherPeriodsOverlap([]));
        Assert.False(GroupPeriods.AnyTeacherPeriodsOverlap([Teacher(startDate: Day(1))]));
        Assert.False(GroupPeriods.AnyTeacherPeriodsOverlap([
            Teacher(startDate: Day(1), endDate: Day(10)), Teacher(startDate: Day(10), endDate: Day(20)),
            Teacher(startDate: Day(20))
        ]));
        Assert.True(GroupPeriods.AnyTeacherPeriodsOverlap([
            Teacher(startDate: Day(1), endDate: Day(10)), Teacher(startDate: Day(10), endDate: Day(20)),
            Teacher(startDate: Day(5), endDate: Day(6))
        ]));
    }

    [Fact]
    public void AnyDayTimePlacePeriodsOverlap_ComparesOnlyTheSameWeekDay()
    {
        Assert.False(GroupPeriods.AnyDayTimePlacePeriodsOverlap([]));
        Assert.False(GroupPeriods.AnyDayTimePlacePeriodsOverlap([
            DayTimePlace(weekDayId: 1, startDate: Day(1)), DayTimePlace(weekDayId: 2, startDate: Day(1))
        ]));
        Assert.False(GroupPeriods.AnyDayTimePlacePeriodsOverlap([
            DayTimePlace(weekDayId: 1, startDate: Day(1), endDate: Day(15)),
            DayTimePlace(weekDayId: 2, startDate: Day(1)), DayTimePlace(weekDayId: 1, startDate: Day(15))
        ]));
        Assert.True(GroupPeriods.AnyDayTimePlacePeriodsOverlap([
            DayTimePlace(weekDayId: 1, startDate: Day(1), endDate: Day(15)),
            DayTimePlace(weekDayId: 2, startDate: Day(1)), DayTimePlace(weekDayId: 1, startDate: Day(14))
        ]));
    }
}
