using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.GroupRowsSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class TransitionReportsTests
{
    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static void AssertRows(ReportTable table, params string[] expected)
    {
        Assert.Equal(expected,
            Rows(table).Select(r => string.Join("|", r.Select(c => c is DateTime d ? d.ToString("MM-dd", CultureInfo.InvariantCulture) : c))));
    }

    // a student's row of a course ends, the next row of the same course (any group) starts later: the days between
    // have no lessons; a row whose next one starts on its end day, or that has no next one, is fine
    [Fact]
    public void StudentMissedTransitions_GapToTheNextRowOfTheCourse_IsListed()
    {
        // Arrange: groups 1 and 2 are Math, 3 is Art
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1", 3).Group(2, "A2", 3)
            .Group(3, "B1", 4, "Art").Student(1, 1, 20, Day(2026, 1, 5), Day(2026, 1, 20))
            .Student(2, 2, 20, Day(2026, 1, 25), Day(2026, 2, 1)).Student(3, 1, 20, Day(2026, 2, 1), Day(2026, 2, 10))
            .Student(4, 3, 20, Day(2026, 2, 20), null).StudentName(20, "Alpha", "Ann", "6.020").Build();

        // Act
        ReportTable table = TransitionReports.StudentMissedTransitions(snapshot);

        // Assert
        Assert.Equal(["courseName", "student", "startDate", "endDate", "nextStartDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["საგანი", "მოსწავლე", "დაწყება", "დასრულება", "შემდეგის დაწყება"],
            table.Columns.Select(c => c.Caption));
        Assert.Equal([ReportColumnTypes.Text, ReportColumnTypes.Text, ReportColumnTypes.Date, ReportColumnTypes.Date,
            ReportColumnTypes.Date], table.Columns.Select(c => c.Type));
        AssertRows(table, "Math|Alpha Ann / 6.020|01-05|01-20|01-25");
        Assert.Equal(["სულ:", 1, null, null, null], Assert.Single(table.FooterRows));
    }

    // Access's GROUP BY: two equal rows are one line; a row that starts on its end day is its own next row
    [Fact]
    public void StudentMissedTransitions_EqualRowsAndSameDayRow()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").Group(2, "A2")
            .Student(1, 1, 20, Day(2026, 1, 5), Day(2026, 1, 20)).Student(2, 2, 20, Day(2026, 1, 5), Day(2026, 1, 20))
            .Student(3, 2, 20, Day(2026, 1, 25), null).Student(4, 1, 21, Day(2026, 3, 1), Day(2026, 3, 1))
            .Student(5, 1, 21, Day(2026, 3, 10), null).Build();

        // Act & Assert
        AssertRows(TransitionReports.StudentMissedTransitions(snapshot), "Math|SLast20 SFirst20 / 6.020|01-05|01-20|01-25");
    }

    // order: student "last first / number" (Access's ORDER BY), then course
    [Fact]
    public void StudentMissedTransitions_OrdersByStudentAndCourse()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1", 3).Group(2, "B1", 4, "Art")
            .Student(1, 1, 20, Day(2026, 1, 5), Day(2026, 1, 20)).Student(2, 1, 20, Day(2026, 2, 1), null)
            .Student(3, 2, 20, Day(2026, 1, 5), Day(2026, 1, 20)).Student(4, 2, 20, Day(2026, 2, 1), null)
            .Student(5, 1, 21, Day(2026, 1, 5), Day(2026, 1, 20)).Student(6, 1, 21, Day(2026, 2, 1), null)
            .StudentName(20, "Zeta", "Z", "6.020").StudentName(21, "Alpha", "A", "6.021").Build();

        // Act & Assert
        AssertRows(TransitionReports.StudentMissedTransitions(snapshot), "Math|Alpha A / 6.021|01-05|01-20|02-01",
            "Art|Zeta Z / 6.020|01-05|01-20|02-01", "Math|Zeta Z / 6.020|01-05|01-20|02-01");
    }

    // any teacher of the group continues it (D123): a gap to the next teacher row is listed, also between two
    // teachers; a handover on the end day is fine, even if the first teacher returns later
    [Fact]
    public void TeacherMissedTransitions_GapToTheGroupsNextTeacherRow_IsListed()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").Group(2, "B1")
            .Teacher(1, 1, 5, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(2, 1, 6, Day(2026, 1, 25), Day(2026, 2, 1))
            .Teacher(3, 1, 7, Day(2026, 2, 1), null).Teacher(4, 2, 5, Day(2026, 1, 5), Day(2026, 1, 20))
            .Teacher(5, 2, 6, Day(2026, 1, 20), null).Teacher(6, 2, 5, Day(2026, 3, 1), null)
            .TeacherContract(5, "Alpha", "Ann", "T5", 1).Build();

        // Act
        ReportTable table = TransitionReports.TeacherMissedTransitions(snapshot);

        // Assert
        Assert.Equal(["groupCode", "teacher", "startDate", "endDate", "nextStartDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "მასწავლებელი", "დაწყება", "დასრულება", "შემდეგის დაწყება"],
            table.Columns.Select(c => c.Caption));
        AssertRows(table, "A1|Alpha Ann / T5|01-05|01-20|01-25");
        Assert.Equal(["სულ:", 1, null, null, null], Assert.Single(table.FooterRows));
    }

    // order: teacher (Access's ORDER BY), then group code
    [Fact]
    public void TeacherMissedTransitions_OrdersByTeacherAndGroup()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1").Group(3, "C1")
            .Teacher(1, 1, 5, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(2, 1, 5, Day(2026, 2, 1), null)
            .Teacher(3, 2, 5, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(4, 2, 5, Day(2026, 2, 1), null)
            .Teacher(5, 3, 6, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(6, 3, 6, Day(2026, 2, 1), null)
            .TeacherContract(5, "Zeta", "Z", "T5", 1).TeacherContract(6, "Alpha", "A", "T6", 1).Build();

        // Act & Assert
        AssertRows(TransitionReports.TeacherMissedTransitions(snapshot), "C1|Alpha A / T6|01-05|01-20|02-01",
            "A1|Zeta Z / T5|01-05|01-20|02-01", "B1|Zeta Z / T5|01-05|01-20|02-01");
    }

    // any schedule row of the group, whatever the week day (Access); rows of other groups are not the next ones
    [Fact]
    public void DayTimeMissedTransitions_GapToTheGroupsNextScheduleRow_IsListed()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .DayTime(1, 1, Day(2026, 1, 5), Day(2026, 1, 20)).DayTime(2, 1, Day(2026, 1, 27), Day(2026, 2, 3))
            .DayTime(3, 1, Day(2026, 2, 3), null).DayTime(4, 2, Day(2026, 1, 5), Day(2026, 1, 25))
            .DayTime(5, 2, Day(2026, 1, 5), Day(2026, 1, 25)).DayTime(6, 2, Day(2026, 2, 1), null)
            .DayTime(7, 1, Day(2026, 1, 21), null).Build();

        // Act
        ReportTable table = TransitionReports.DayTimeMissedTransitions(snapshot);

        // Assert
        Assert.Equal(["groupCode", "startDate", "endDate", "nextStartDate"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "დაწყება", "დასრულება", "შემდეგის დაწყება"], table.Columns.Select(c => c.Caption));
        AssertRows(table, "A1|01-05|01-25|02-01", "B1|01-05|01-20|01-21");
        Assert.Equal(["სულ:", 2, null, null], Assert.Single(table.FooterRows));
    }

    // r26 ties: one student and course by the start and then the end; two contracts with one name and number
    // (another year) by the contract
    [Fact]
    public void StudentMissedTransitions_TiesByStartEndAndContract()
    {
        // Arrange: contract 20 has gap rows Feb 10–20, Jan 5–22 and Jan 5–20 (groups 1 and 2 are both Math);
        // contract 21 has the same name and number and a gap row Jan 5–20 before April
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").Group(2, "A2")
            .Student(1, 2, 20, Day(2026, 2, 10), Day(2026, 2, 20)).Student(2, 1, 20, Day(2026, 1, 5), Day(2026, 1, 22))
            .Student(3, 2, 20, Day(2026, 1, 5), Day(2026, 1, 20)).Student(4, 1, 20, Day(2026, 3, 1), null)
            .Student(5, 1, 21, Day(2026, 1, 5), Day(2026, 1, 20)).Student(6, 1, 21, Day(2026, 4, 1), null)
            .StudentName(21, "Alpha", "A", "6.020").StudentName(20, "Alpha", "A", "6.020").Build();

        // Act & Assert
        AssertRows(TransitionReports.StudentMissedTransitions(snapshot), "Math|Alpha A / 6.020|01-05|01-20|02-10",
            "Math|Alpha A / 6.020|01-05|01-20|04-01", "Math|Alpha A / 6.020|01-05|01-22|02-10",
            "Math|Alpha A / 6.020|02-10|02-20|03-01");
    }

    // r27 ties: one teacher in two groups with one code (another year) by the group; in one group by the start and
    // then the end
    [Fact]
    public void TeacherMissedTransitions_TiesByGroupStartAndEnd()
    {
        // Arrange: groups 2 and 1 share the code A1
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(2, "A1").Group(1, "A1")
            .Teacher(1, 2, 5, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(2, 2, 6, Day(2026, 2, 1), null)
            .Teacher(3, 1, 5, Day(2026, 2, 10), Day(2026, 2, 20)).Teacher(4, 1, 5, Day(2026, 1, 5), Day(2026, 1, 22))
            .Teacher(5, 1, 5, Day(2026, 1, 5), Day(2026, 1, 20)).Teacher(6, 1, 7, Day(2026, 3, 1), null).Build();

        // Act & Assert
        AssertRows(TransitionReports.TeacherMissedTransitions(snapshot), "A1|TLast5 TFirst5 / T5|01-05|01-20|02-10",
            "A1|TLast5 TFirst5 / T5|01-05|01-22|02-10", "A1|TLast5 TFirst5 / T5|02-10|02-20|03-01",
            "A1|TLast5 TFirst5 / T5|01-05|01-20|02-01");
    }

    // r28 ties: two groups with one code (another year) by the group; in one group by the start and then the end
    [Fact]
    public void DayTimeMissedTransitions_TiesByGroupStartAndEnd()
    {
        // Arrange: groups 2 and 1 share the code B1
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(2, "B1").Group(1, "B1")
            .DayTime(1, 2, Day(2026, 1, 5), Day(2026, 1, 20)).DayTime(2, 2, Day(2026, 2, 1), null)
            .DayTime(3, 1, Day(2026, 2, 10), Day(2026, 2, 20)).DayTime(4, 1, Day(2026, 1, 5), Day(2026, 1, 22))
            .DayTime(5, 1, Day(2026, 1, 5), Day(2026, 1, 20)).DayTime(6, 1, Day(2026, 3, 1), null).Build();

        // Act & Assert
        AssertRows(TransitionReports.DayTimeMissedTransitions(snapshot), "B1|01-05|01-20|02-10", "B1|01-05|01-22|02-10",
            "B1|02-10|02-20|03-01", "B1|01-05|01-20|02-01");
    }
}
