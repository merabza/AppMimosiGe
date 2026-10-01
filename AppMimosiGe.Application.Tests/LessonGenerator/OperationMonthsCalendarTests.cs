using System;
using AppMimosiGe.Application.LessonGenerator.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class OperationMonthsCalendarTests
{
    private static DateTime Date(int year, int month, int day, int hour = 0)
    {
        return new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);
    }

    [Fact]
    public void MonthsToAdd_CalendarAlreadyLongerThanTwoMonths_AddsNothing()
    {
        Assert.Empty(OperationMonthsCalendar.MonthsToAdd(Date(2027, 11, 1), Date(2026, 10, 1, 9)));
    }

    [Fact]
    public void MonthsToAdd_AddsTheMonthsUpToTheMonthOfTodayPlusTwoMonths()
    {
        Assert.Equal([Date(2026, 11, 1), Date(2026, 12, 1)],
            OperationMonthsCalendar.MonthsToAdd(Date(2026, 10, 1), Date(2026, 10, 15, 9)));
    }

    [Fact]
    public void MonthsToAdd_EndOfTheMonth_UsesTheShorterTargetMonth()
    {
        //31 December + 2 months is 28 February
        Assert.Equal([Date(2027, 1, 1), Date(2027, 2, 1)],
            OperationMonthsCalendar.MonthsToAdd(Date(2026, 12, 1), Date(2026, 12, 31, 23)));
    }

    [Fact]
    public void MonthsToAdd_EmptyCalendar_AddsOnlyTheTargetMonth()
    {
        Assert.Equal([Date(2026, 12, 1)], OperationMonthsCalendar.MonthsToAdd(null, Date(2026, 10, 15)));
    }

    [Fact]
    public void HorizonEnd_IsTheLastDayOfTheMonth()
    {
        Assert.Equal(Date(2027, 11, 30), OperationMonthsCalendar.HorizonEnd(Date(2027, 11, 1)));
        Assert.Equal(Date(2027, 2, 28), OperationMonthsCalendar.HorizonEnd(Date(2027, 2, 1)));
        Assert.Equal(Date(2026, 12, 31), OperationMonthsCalendar.HorizonEnd(Date(2026, 12, 15)));
    }
}
