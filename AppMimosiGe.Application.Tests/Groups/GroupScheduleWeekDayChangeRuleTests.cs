using System;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

//D181: the week day of a schedule row that started before today is not changed in place
public sealed class GroupScheduleWeekDayChangeRuleTests
{
    //the day after the saved rows start (StartDate, 15.09.2026)
    private static readonly DateTime NextDay = StartDate.AddDays(1);

    private static readonly DateTime October1 = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);

    //group 42 with the saved Monday row 300 and Thursday row 301, both from StartDate
    private static Group SavedGroup()
    {
        var group = new Group { GrpId = 42, GroupCode = "1001" };
        group.GroupDayTimePlaces.Add(SavedRow(300, 1));
        group.GroupDayTimePlaces.Add(SavedRow(301, 4));
        return group;
    }

    private static GroupDayTimePlace SavedRow(int gdtpId, int weekDayId) => new()
    {
        GdtpId = gdtpId,
        GroupId = 42,
        WeekDayId = weekDayId,
        LessonStartTimeId = 17,
        HoursCount = 1.5f,
        RoomId = 2,
        StartDate = StartDate
    };

    private static bool ChangesStartedRowWeekDay(DateTime today, params GroupDayTimePlaceRequest[] rows) =>
        GroupScheduleWeekDayChangeRule.ChangesStartedRowWeekDay(SavedGroup(), ValidRequest(dayTimePlaces: [.. rows]),
            today);

    [Fact]
    public void StartedRowWithAnotherWeekDay_IsAChange()
    {
        Assert.True(ChangesStartedRowWeekDay(October1, DayTimePlace(300), DayTimePlace(301, 5)));
    }

    // only today's date counts: a row that started yesterday is locked from midnight on
    [Fact]
    public void RowThatStartedYesterday_IsLocked()
    {
        Assert.True(ChangesStartedRowWeekDay(NextDay, DayTimePlace(300, 3)));
    }

    // a row starting today has no past lessons yet, so its week day can still be corrected
    [Fact]
    public void RowStartingToday_MayChangeItsWeekDay()
    {
        Assert.False(ChangesStartedRowWeekDay(NextDay.AddTicks(-1), DayTimePlace(300, 3), DayTimePlace(301, 5)));
    }

    [Fact]
    public void RowStartingLater_MayChangeItsWeekDay()
    {
        Assert.False(ChangesStartedRowWeekDay(StartDate.AddDays(-1), DayTimePlace(300, 3)));
    }

    [Fact]
    public void StartedRowWithItsWeekDay_MayChangeTheOtherFields()
    {
        Assert.False(ChangesStartedRowWeekDay(October1,
            DayTimePlace(300, 1, 20, 2, 3, StartDate.AddDays(7), October1.AddMonths(1)), DayTimePlace(301, 4)));
    }

    // the right way: the row ends and a new row of the new week day starts that day
    [Fact]
    public void EndedRowAndANewRow_AreNoChange()
    {
        Assert.False(ChangesStartedRowWeekDay(October1, DayTimePlace(300, endDate: October1),
            DayTimePlace(weekDayId: 3, startDate: October1), DayTimePlace(301, 4)));
    }

    // a removed row does not change its week day: RemovesStartedRow checks the removal
    [Fact]
    public void RemovedRow_IsNoWeekDayChange()
    {
        Assert.False(ChangesStartedRowWeekDay(October1, DayTimePlace(weekDayId: 3)));
    }

    // the saved start counts: moving the start into the future does not unlock the row
    [Fact]
    public void StartedRowMovedToTheFutureWithAnotherWeekDay_IsAChange()
    {
        Assert.True(ChangesStartedRowWeekDay(October1, DayTimePlace(300, 3, startDate: October1.AddMonths(1))));
    }

    // a removed row that started yesterday, even one that has already ended, is locked
    [Fact]
    public void RemovedRowThatStartedYesterday_IsLocked()
    {
        GroupDayTimePlace ended = SavedRow(301, 4);
        ended.EndDate = NextDay;

        Assert.True(GroupScheduleWeekDayChangeRule.RemovesStartedRow([ended], NextDay));
        Assert.True(GroupScheduleWeekDayChangeRule.RemovesStartedRow([SavedRow(300, 1)], NextDay));
    }

    // a row starting today or later has no past lessons yet, so it may still be removed
    [Fact]
    public void RemovedRowStartingTodayOrLater_IsAllowed()
    {
        Assert.False(GroupScheduleWeekDayChangeRule.RemovesStartedRow([SavedRow(300, 1)], NextDay.AddTicks(-1)));
        Assert.False(GroupScheduleWeekDayChangeRule.RemovesStartedRow([SavedRow(300, 1)], StartDate.AddDays(-1)));
    }

    [Fact]
    public void NoRemovedRows_AreAllowed()
    {
        Assert.False(GroupScheduleWeekDayChangeRule.RemovesStartedRow([], October1));
    }
}
