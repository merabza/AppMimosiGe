using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class TimeSheetReportTests
{
    private const int FirstHalfIndex = 18;
    private const int SecondHalfIndex = 35;

    private static readonly Dictionary<int, string> MonthNames = new()
    {
        [9] = "სექტემბერი", [10] = "ოქტომბერი"
    };

    //employees: 5 "TLast5 TFirst5" T5, 6 "Alpha A" T6 and 7 "Alpha A" T10 (one name, ordered by the number)
    private static readonly Dictionary<int, SchedulePerson> Employees = new()
    {
        [5] = new SchedulePerson("TLast5", "TFirst5", "T5"),
        [6] = new SchedulePerson("Alpha", "A", "T6"),
        [7] = new SchedulePerson("Alpha", "A", "T10")
    };

    private static DateTime At(int month, int day, int hour, int minute = 0, int second = 0)
    {
        return new DateTime(2026, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }

    private static WorkTimeLesson Lesson(int employeeId, DateTime lessonDt, float hours, DateTime? recoverDate = null)
    {
        return new WorkTimeLesson(lessonDt.Day, employeeId, lessonDt, recoverDate, hours);
    }

    private static WorkTimeRecord Record(int employeeId, DateTime start, DateTime end)
    {
        return new WorkTimeRecord(start.Day, employeeId, start, end);
    }

    private static string Number(object? value)
    {
        return value is null
            ? ""
            : Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.####", CultureInfo.InvariantCulture);
    }

    //"№|name|number|day=hours,…|I half|II half|days|hours"
    private static string Row(ReportTable table, List<object?> row)
    {
        IEnumerable<string> days = table.Columns.Select((column, index) => (column, index))
            .Where(c => c.column.Name.StartsWith("day", StringComparison.Ordinal) && c.column.Name != "days" &&
                        row[c.index] is not null)
            .Select(c => $"{c.column.Caption}={Number(row[c.index])}");
        return $"{row[0]}|{row[1]}|{row[2]}|{string.Join(",", days)}|{Number(row[FirstHalfIndex])}|" +
               $"{Number(row[SecondHalfIndex])}|{row[36]}|{Number(row[37])}";
    }

    private static string[] Rows(ReportTable table, int section)
    {
        return [.. table.Sections[section].Rows.Select(row => Row(table, row))];
    }

    // the form's columns: №, name, number, days 1–15, the first half, days 16–31, the second half, days, hours
    [Fact]
    public void TimeSheet_HasTheFormsColumns()
    {
        // Act
        ReportTable table = TimeSheetReport.TimeSheet(new WorkTimeData([], [], Employees), MonthNames);

        // Assert
        Assert.Equal([
            "number", "employee", "contractNumber", .. Enumerable.Range(1, 15).Select(d => $"day{d}"), "firstHalf",
            .. Enumerable.Range(16, 16).Select(d => $"day{d}"), "secondHalf", "days", "hours"
        ], table.Columns.Select(c => c.Name));
        Assert.Equal([
            "№", "გვარი, სახელი", "ტაბელის ნომერი", .. Enumerable.Range(1, 15).Select(d => $"{d}"), "I ნახევარი",
            .. Enumerable.Range(16, 16).Select(d => $"{d}"), "II ნახევარი", "სულ დღე", "სულ საათი"
        ], table.Columns.Select(c => c.Caption));
        Assert.Equal([
            "wholeNumber", "text", "text", .. Enumerable.Repeat("number", 33), "wholeNumber", "number"
        ], table.Columns.Select(c => c.Type));
        Assert.Empty(table.Sections);
        Assert.Empty(table.FooterRows);
    }

    // a day's hours are the union of its lessons and work records: time in two of them counts once
    [Fact]
    public void TimeSheet_CountsSimultaneousTimeOnce()
    {
        // Arrange
        var data = new WorkTimeData([
            Lesson(5, At(9, 1, 13, 30), 1.5f), Lesson(5, At(9, 1, 16), 1f), Lesson(5, At(9, 2, 10), 2f),
            Lesson(5, At(9, 2, 10, 30), 1f), Lesson(5, At(9, 16, 18), 1.5f)
        ], [Record(5, At(9, 1, 12), At(9, 1, 14))], Employees);

        // Act
        ReportTable table = TimeSheetReport.TimeSheet(data, MonthNames);

        // Assert
        Assert.Equal("სექტემბერი 2026", Assert.Single(table.Sections).Header);
        Assert.Equal(["1|TLast5 TFirst5|T5|1=4,2=2,16=1.5|6|1.5|3|7.5"], Rows(table, 0));
        Assert.Null(table.Sections[0].Footer);
    }

    // a recovered lesson counts on the recovery day at the lesson's own time; an empty or reversed record does not
    // count; a record over midnight counts on its first day
    [Fact]
    public void TimeSheet_RecoveredLessonsAndRecords()
    {
        // Arrange
        var data = new WorkTimeData([
            Lesson(5, At(9, 19, 10, 30), 1.5f, At(9, 22, 0)), Lesson(5, At(9, 22, 11), 1f)
        ], [
            Record(5, At(9, 3, 12), At(9, 3, 11)), Record(5, At(9, 4, 12), At(9, 4, 12)),
            Record(5, At(9, 30, 22), At(10, 1, 2))
        ], Employees);

        // Act & Assert
        Assert.Equal(["1|TLast5 TFirst5|T5|22=1.5,30=4|0|5.5|2|5.5"],
            Rows(TimeSheetReport.TimeSheet(data, MonthNames), 0));
    }

    // a section per month; the employees by name, then by number, numbered from 1 in each month; hours with 2
    // decimals (1:20 → 1.33); a day of a few seconds shows 0 hours and is not a worked day
    [Fact]
    public void TimeSheet_MonthsEmployeesAndRounding()
    {
        // Arrange
        var data = new WorkTimeData([
            Lesson(7, At(9, 5, 10), 1f), Lesson(5, At(10, 2, 10), 2f), Lesson(5, At(9, 20, 10), 1f)
        ], [Record(6, At(9, 1, 9), At(9, 1, 10, 20)), Record(7, At(9, 10, 10), At(9, 10, 10, 0, 10))], Employees);

        // Act
        ReportTable table = TimeSheetReport.TimeSheet(data, MonthNames);

        // Assert
        Assert.Equal(["სექტემბერი 2026", "ოქტომბერი 2026"], table.Sections.Select(s => s.Header));
        Assert.Equal([
            "1|Alpha A|T10|5=1,10=0|1|0|1|1", "2|Alpha A|T6|1=1.33|1.33|0|1|1.33", "3|TLast5 TFirst5|T5|20=1|0|1|1|1"
        ], Rows(table, 0));
        Assert.Equal(["1|TLast5 TFirst5|T5|2=2|2|0|1|2"], Rows(table, 1));
    }

    // the first half is days 1–15, the second days 16–31
    [Fact]
    public void TimeSheet_HalvesSplitAfterTheFifteenth()
    {
        // Arrange
        var data = new WorkTimeData([Lesson(5, At(9, 15, 10), 2f), Lesson(5, At(9, 16, 10), 1f)], [], Employees);

        // Act & Assert
        Assert.Equal(["1|TLast5 TFirst5|T5|15=2,16=1|2|1|2|3"], Rows(TimeSheetReport.TimeSheet(data, MonthNames), 0));
    }

    // a lesson that starts inside an earlier, longer interval adds nothing; one that goes beyond adds the rest
    [Fact]
    public void TimeSheet_UnionOfManyIntervals()
    {
        // Arrange
        var data = new WorkTimeData([
            Lesson(5, At(9, 7, 10), 3f), Lesson(5, At(9, 7, 10, 30), 1f), Lesson(5, At(9, 7, 12), 2f),
            Lesson(5, At(9, 7, 15), 1f), Lesson(5, At(9, 7, 15), 0.5f)
        ], [], Employees);

        // Act & Assert
        Assert.Equal(["1|TLast5 TFirst5|T5|7=5|5|0|1|5"], Rows(TimeSheetReport.TimeSheet(data, MonthNames), 0));
    }
}
