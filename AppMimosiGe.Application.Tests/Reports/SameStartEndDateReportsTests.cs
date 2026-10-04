using System;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.GroupRowsSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class SameStartEndDateReportsTests
{
    //rows that start and end on one day: such a row is active on no day; a reversed or a one-day row is not listed
    //(Access compared StartDate = EndDate only)
    private static GroupRowsSnapshot Snapshot()
    {
        return new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .Student(1, 1, 20, Day(2026, 2, 15), Day(2026, 2, 15)).Student(2, 1, 21, Day(2026, 2, 15), Day(2026, 2, 16))
            .Student(3, 2, 21, Day(2026, 3, 1), Day(2026, 3, 1)).Student(4, 2, 22, Day(2026, 3, 2), Day(2026, 3, 1))
            .Student(5, 2, 22, Day(2026, 3, 2), null).StudentName(20, "Zeta", "Z", "6.020")
            .StudentName(21, "Alpha", "A", "6.021").Teacher(1, 1, 5, Day(2026, 2, 15), Day(2026, 2, 15))
            .Teacher(2, 2, 6, Day(2026, 2, 15), Day(2026, 2, 15)).Teacher(3, 2, 5, Day(2026, 2, 15), null)
            .TeacherContract(5, "Zeta", "Z", "T5", 1).TeacherContract(6, "Alpha", "A", "T6", 1)
            .DayTime(1, 1, Day(2026, 2, 15), Day(2026, 2, 15)).DayTime(2, 2, Day(2026, 3, 1), Day(2026, 3, 1))
            .DayTime(3, 2, Day(2026, 2, 1), Day(2026, 2, 1)).DayTime(4, 2, Day(2026, 2, 1), Day(2026, 2, 2)).Build();
    }

    private static void AssertRows(ReportTable table, params string[] expected)
    {
        Assert.Equal(expected, Assert.Single(table.Sections).Rows.Select(r =>
            string.Join("|", r.Select(c => c is DateTime d ? d.ToString("MM-dd", CultureInfo.InvariantCulture) : c))));
    }

    // order: student (Access's ORDER BY), then group code
    [Fact]
    public void StudentSameStartEndDate_ListsTheStudentRows()
    {
        // Act
        ReportTable table = SameStartEndDateReports.StudentSameStartEndDate(Snapshot());

        // Assert
        Assert.Equal(["groupCode", "student", "startDate", "endDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "მოსწავლე", "დაწყება", "დასრულება"], table.Columns.Select(c => c.Caption));
        Assert.Equal([ReportColumnTypes.Text, ReportColumnTypes.Text, ReportColumnTypes.Date, ReportColumnTypes.Date],
            table.Columns.Select(c => c.Type));
        AssertRows(table, "A1|Alpha A / 6.021|03-01|03-01", "B1|Zeta Z / 6.020|02-15|02-15");
        Assert.Equal(["სულ:", 2, null, null], Assert.Single(table.FooterRows));
    }

    // order: teacher (Access's ORDER BY), then group code
    [Fact]
    public void TeacherSameStartEndDate_ListsTheTeacherRows()
    {
        // Act
        ReportTable table = SameStartEndDateReports.TeacherSameStartEndDate(Snapshot());

        // Assert
        Assert.Equal(["groupCode", "teacher", "startDate", "endDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "მასწავლებელი", "დაწყება", "დასრულება"], table.Columns.Select(c => c.Caption));
        AssertRows(table, "A1|Alpha A / T6|02-15|02-15", "B1|Zeta Z / T5|02-15|02-15");
        Assert.Equal(["სულ:", 2, null, null], Assert.Single(table.FooterRows));
    }

    // order: group code, then the start date
    [Fact]
    public void DayTimeSameStartEndDate_ListsTheScheduleRows()
    {
        // Act
        ReportTable table = SameStartEndDateReports.DayTimeSameStartEndDate(Snapshot());

        // Assert
        Assert.Equal(["groupCode", "startDate", "endDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "დაწყება", "დასრულება"], table.Columns.Select(c => c.Caption));
        AssertRows(table, "A1|02-01|02-01", "A1|03-01|03-01", "B1|02-15|02-15");
        Assert.Equal(["სულ:", 3, null], Assert.Single(table.FooterRows));
    }

    // one student's rows: by the group code, then by the date
    [Fact]
    public void StudentSameStartEndDate_OneStudentByGroupAndDate()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .Student(1, 1, 20, Day(2026, 3, 1), Day(2026, 3, 1)).Student(2, 2, 20, Day(2026, 3, 5), Day(2026, 3, 5))
            .Student(3, 2, 20, Day(2026, 2, 1), Day(2026, 2, 1)).StudentName(20, "Alpha", "A", "6.020").Build();

        // Act & Assert
        AssertRows(SameStartEndDateReports.StudentSameStartEndDate(snapshot), "A1|Alpha A / 6.020|02-01|02-01",
            "A1|Alpha A / 6.020|03-05|03-05", "B1|Alpha A / 6.020|03-01|03-01");
    }

    // one teacher's rows: by the group code, then by the date
    [Fact]
    public void TeacherSameStartEndDate_OneTeacherByGroupAndDate()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .Teacher(1, 1, 5, Day(2026, 3, 1), Day(2026, 3, 1)).Teacher(2, 2, 5, Day(2026, 3, 5), Day(2026, 3, 5))
            .Teacher(3, 2, 5, Day(2026, 2, 1), Day(2026, 2, 1)).TeacherContract(5, "Alpha", "A", "T5", 1).Build();

        // Act & Assert
        AssertRows(SameStartEndDateReports.TeacherSameStartEndDate(snapshot), "A1|Alpha A / T5|02-01|02-01",
            "A1|Alpha A / T5|03-05|03-05", "B1|Alpha A / T5|03-01|03-01");
    }

    [Fact]
    public void SameStartEndDate_NoSuchRows_OnlyTheZeroTotal()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1")
            .Student(1, 1, 20, Day(2026, 2, 15), null).Build();

        // Act
        ReportTable table = SameStartEndDateReports.StudentSameStartEndDate(snapshot);

        // Assert
        Assert.Empty(Assert.Single(table.Sections).Rows);
        Assert.Equal(["სულ:", 0, null, null], Assert.Single(table.FooterRows));
    }
}
