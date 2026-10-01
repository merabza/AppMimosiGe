using System;
using AppMimosiGe.Application.Balances.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class DesiredPayDatesTests
{
    private static DateTime Day(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //Sgn(Day - D) = -1: this month's D, the next one a month later plus a day
    [Fact]
    public void Calculate_TodayBeforeTheDesiredDay_IsThisMonth()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 10, 15), Day(2026, 11, 16)),
            DesiredPayDates.Calculate(Day(2026, 10, 1), 15));
    }

    //Sgn = 0: (0 + 1) / 2 = 0.5 month, which DateAdd truncates to 0: still today
    [Fact]
    public void Calculate_TodayIsTheDesiredDay_IsToday()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 10, 15), Day(2026, 11, 16)),
            DesiredPayDates.Calculate(Day(2026, 10, 15), 15));
    }

    //Sgn = 1: next month's D, the one after it two months later plus a day
    [Fact]
    public void Calculate_TodayAfterTheDesiredDay_IsNextMonth()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 11, 15), Day(2026, 12, 16)),
            DesiredPayDates.Calculate(Day(2026, 10, 16), 15));
    }

    //DateSerial(2026, 9, 31) is 1 October
    [Fact]
    public void Calculate_Day31InAThirtyDayMonth_OverflowsIntoTheNextMonth()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 10, 1), Day(2026, 11, 2)),
            DesiredPayDates.Calculate(Day(2026, 9, 10), 31));
    }

    //DateAdd("m") stops at the end of a shorter month: 31.01 + 1 month = 28.02
    [Fact]
    public void Calculate_Day31OnThe31st_StopsAtTheEndOfFebruary()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 1, 31), Day(2026, 3, 1)),
            DesiredPayDates.Calculate(Day(2026, 1, 31), 31));
    }

    [Fact]
    public void Calculate_AcrossTheYearEnd_GoesToJanuary()
    {
        Assert.Equal(new DesiredPayDates(Day(2027, 1, 5), Day(2027, 2, 6)),
            DesiredPayDates.Calculate(Day(2026, 12, 20), 5));
    }

    [Fact]
    public void Calculate_TimeOfToday_IsIgnored()
    {
        Assert.Equal(new DesiredPayDates(Day(2026, 10, 15), Day(2026, 11, 16)),
            DesiredPayDates.Calculate(Day(2026, 10, 15).AddHours(17), 15));
    }
}
