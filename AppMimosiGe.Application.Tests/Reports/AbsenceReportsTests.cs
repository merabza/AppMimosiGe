using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class AbsenceReportsTests
{
    private static DateTime At(int day, int hour)
    {
        return new DateTime(2026, 9, day, hour, 0, 0, DateTimeKind.Unspecified);
    }

    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static string Text(List<object?> row)
    {
        return string.Join("|", row.Select(c => c is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : c));
    }

    //a student with a payer; synthetic people only
    private static StudentContact Contact(int contractId, string lastName, string? phone = "555123456")
    {
        return new StudentContact(new SchedulePerson(lastName, "S", $"6.{contractId:000}"), phone, $"P{contractId} Payer",
            null);
    }

    // r14: the most absences first, then the student and the course
    [Fact]
    public void Missings_OrdersByCountThenStudentAndCourse()
    {
        // Arrange
        List<AbsenceCountRow> counts =
        [
            new(20, new SchedulePerson("Zeta", "Z", "6.020"), "Math", 2),
            new(21, new SchedulePerson("Alpha", "A", "6.021"), "Math", 2),
            new(21, new SchedulePerson("Alpha", "A", "6.021"), "Art", 2),
            new(22, new SchedulePerson("Beta", "B", "6.022"), "Math", 5)
        ];

        // Act
        ReportTable table = AbsenceReports.Missings(counts);

        // Assert
        Assert.Equal(["studentName", "contractNumber", "courseName", "missings"], table.Columns.Select(c => c.Name));
        Assert.Equal(["გვარი და სახელი", "კონტრაქტი", "საგანი", "გაცდენების რაოდენობა"],
            table.Columns.Select(c => c.Caption));
        Assert.Equal(ReportColumnTypes.WholeNumber, table.Columns[3].Type);
        Assert.Equal(["Beta B|6.022|Math|5", "Alpha A|6.021|Art|2", "Alpha A|6.021|Math|2", "Zeta Z|6.020|Math|2"],
            Rows(table).Select(Text));
        Assert.Empty(table.FooterRows);
    }

    // two students with one name and one count: by the contract number
    [Fact]
    public void Missings_OneNameAndCount_ByContractNumber()
    {
        // Arrange
        List<AbsenceCountRow> counts =
        [
            new(21, new SchedulePerson("Alpha", "A", "6.021"), "Math", 2),
            new(20, new SchedulePerson("Alpha", "A", "6.020"), "Math", 2)
        ];

        // Act & Assert
        Assert.Equal(["Alpha A|6.020|Math|2", "Alpha A|6.021|Math|2"], Rows(AbsenceReports.Missings(counts)).Select(Text));
    }

    // r17: the absences after the last presence count; a student never present counts every absence (D122); only
    // more than one is listed. Order: the count, the most first, then the student
    [Fact]
    public void MissingsInRow_CountsTheAbsencesAfterTheLastPresence()
    {
        // Arrange: 20 present on the 5th: 2 later absences; 21 never present: 2; 22 present on the 9th: 1 later
        // absence; 23 present on the 10th: none later
        MissingsInRowData data = new([
            new StudentAbsence(20, At(1, 10)), new StudentAbsence(20, At(8, 10)), new StudentAbsence(20, At(9, 10)),
            new StudentAbsence(21, At(2, 10)), new StudentAbsence(21, At(3, 10)), new StudentAbsence(22, At(8, 10)),
            new StudentAbsence(22, At(10, 10)), new StudentAbsence(23, At(9, 10)), new StudentAbsence(23, At(8, 10))
        ], new Dictionary<int, DateTime> { [20] = At(5, 10), [22] = At(9, 10), [23] = At(10, 10) }, new Dictionary<int, StudentContact>
        {
            [20] = Contact(20, "Zeta"), [21] = Contact(21, "Alpha", null), [22] = Contact(22, "Beta"),
            [23] = Contact(23, "Gamma")
        });

        // Act
        ReportTable table = AbsenceReports.MissingsInRow(data);

        // Assert
        Assert.Equal(["student", "studentPhone", "payer", "payerPhone", "missingsInRow"],
            table.Columns.Select(c => c.Name));
        Assert.Equal(["მოსწავლე", "ტელ. ნომერი", "გადამხდელი", "ტელ. ნომერი", "ზედიზედ გაცდენა"],
            table.Columns.Select(c => c.Caption));
        Assert.Equal(ReportColumnTypes.WholeNumber, table.Columns[4].Type);
        Assert.Equal(["Alpha S / 6.021||P21 Payer||2", "Zeta S / 6.020|555-12-34-56|P20 Payer||2"],
            Rows(table).Select(Text));
        Assert.Equal(["სულ:", 2, null, null, null], Assert.Single(table.FooterRows));
    }

    // an absence at the time of the last presence is not after it (Access's MaxOfLessonDT < LessonDT)
    [Fact]
    public void MissingsInRow_AbsenceAtTheLastPresenceTime_DoesNotCount()
    {
        // Arrange
        MissingsInRowData data = new([
            new StudentAbsence(20, At(5, 10)), new StudentAbsence(20, At(6, 10)), new StudentAbsence(20, At(7, 10))
        ], new Dictionary<int, DateTime> { [20] = At(5, 10) }, new Dictionary<int, StudentContact> { [20] = Contact(20, "Zeta") });

        // Act & Assert
        Assert.Equal(2, Assert.Single(Rows(AbsenceReports.MissingsInRow(data)))[4]);
    }

    // the most absences first
    [Fact]
    public void MissingsInRow_OrdersByCountDescending()
    {
        // Arrange
        MissingsInRowData data = new([
            new StudentAbsence(20, At(1, 10)), new StudentAbsence(20, At(2, 10)), new StudentAbsence(21, At(1, 10)),
            new StudentAbsence(21, At(2, 10)), new StudentAbsence(21, At(3, 10))
        ], new Dictionary<int, DateTime>(), new Dictionary<int, StudentContact>
        {
            [20] = Contact(20, "Alpha"), [21] = Contact(21, "Zeta")
        });

        // Act & Assert
        Assert.Equal([3, 2], Rows(AbsenceReports.MissingsInRow(data)).Select(r => r[4]));
    }

    // Access's "000-00-00-00" for nine digits, anything else as it is
    [Theory]
    [InlineData("555123456", "555-12-34-56")]
    [InlineData("55512345", "55512345")]
    [InlineData("5551234567", "5551234567")]
    [InlineData("55512345a", "55512345a")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void FormatPhone_NineDigits(string? phone, string? expected)
    {
        Assert.Equal(expected, AbsenceReports.FormatPhone(phone));
    }
}
