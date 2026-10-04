using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class WeekDayPivotTests
{
    private static readonly List<ScheduleWeekDay> WeekDays = [new(3, "Wed"), new(1, "Mon"), new(2, "Tue")];

    // a row per key, a cell per week day in the week days' order; an empty cell is null
    [Fact]
    public void Pivot_GroupsByKeyAndWeekDayInTheWeekDaysOrder()
    {
        // Arrange
        (string Key, int Day, int Value)[] items = [("a", 1, 5), ("a", 1, 7), ("a", 3, 2), ("b", 2, 4)];

        // Act
        List<PivotRow<string>> rows = WeekDayPivot.Pivot(items, i => i.Key, i => i.Day,
            cell => cell.Sum(i => i.Value), WeekDays);

        // Assert
        Assert.Equal(2, rows.Count);
        PivotRow<string> a = Assert.Single(rows, r => r.Key == "a");
        Assert.Equal([2, 12, null], a.Days);
        PivotRow<string> b = Assert.Single(rows, r => r.Key == "b");
        Assert.Equal([null, null, 4], b.Days);
    }

    // an item of a day that is not among the week days is not shown
    [Fact]
    public void Pivot_DayOutsideTheWeekDays_IsIgnored()
    {
        // Arrange
        (string Key, int Day)[] items = [("a", 9)];

        // Act
        List<PivotRow<string>> rows = WeekDayPivot.Pivot(items, i => i.Key, i => i.Day, cell => cell.Count, WeekDays);

        // Assert
        Assert.Equal([null, null, null], Assert.Single(rows).Days);
    }

    [Fact]
    public void WeekDayColumns_NamesCaptionsAndTypeInTheWeekDaysOrder()
    {
        // Act
        List<ReportColumnResponse> columns = WeekDayPivot.WeekDayColumns(WeekDays, ReportColumnTypes.Time);

        // Assert
        Assert.Equal([
            new ReportColumnResponse("weekDay3", "Wed", ReportColumnTypes.Time),
            new ReportColumnResponse("weekDay1", "Mon", ReportColumnTypes.Time),
            new ReportColumnResponse("weekDay2", "Tue", ReportColumnTypes.Time)
        ], columns);
    }
}
