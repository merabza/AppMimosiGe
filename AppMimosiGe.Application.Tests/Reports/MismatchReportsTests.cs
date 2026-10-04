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

public sealed class MismatchReportsTests
{
    private static readonly DateTime September = Day(2026, 9, 1);
    private static readonly DateTime October = Day(2026, 10, 1);

    //the academic years' last finish date (Access's vMaxDate): an open end
    private static readonly DateTime MaxFinishDate = Day(2027, 9, 1);

    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static string Text(List<object?> row)
    {
        return string.Join("|", row.Select(c => c switch
        {
            DateTime d => d.ToString("MM-dd", CultureInfo.InvariantCulture),
            float or double or decimal => Convert.ToDecimal(c, CultureInfo.InvariantCulture)
                .ToString("0.####", CultureInfo.InvariantCulture),
            _ => c
        }));
    }

    // a group row's scheme that differs from the contract's main scheme; a contract without a main scheme is not
    // compared (Access's INNER JOIN). Order: teacher, then group code
    [Fact]
    public void TeacherSchemeMismatches_ListsTheRowsOfAnotherScheme()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").Group(2, "B1").Group(3, "C1")
            .Teacher(1, 1, 5, September, null, 2).Teacher(2, 2, 5, September, null).Teacher(3, 3, 6, September, null, 2)
            .Teacher(4, 3, 7, September, Day(2026, 9, 10)).TeacherContract(5, "Zeta", "Z", "T5", 1)
            .TeacherContract(6, "Beta", "B", "T6", null).TeacherContract(7, "Alpha", "A", "T7", 2).Build();

        // Act
        ReportTable table = MismatchReports.TeacherSchemeMismatches(snapshot);

        // Assert
        Assert.Equal(["teacher", "groupCode", "contractScheme", "groupScheme"], table.Columns.Select(c => c.Name));
        Assert.Equal(["მასწავლებელი", "ჯგუფი", "კონტრაქტის სქემა", "ჯგუფის სქემა"], table.Columns.Select(c => c.Caption));
        Assert.All(table.Columns, c => Assert.Equal(ReportColumnTypes.Text, c.Type));
        Assert.Equal(["Alpha A / T7|C1|S2|S1", "Zeta Z / T5|A1|S1|S2"], Rows(table).Select(Text));
        Assert.Empty(table.FooterRows);
    }

    // one teacher's rows: by the group code, then by the group's scheme
    [Fact]
    public void TeacherSchemeMismatches_OneTeacherByGroupAndScheme()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .Teacher(1, 1, 5, September, null, 2).Teacher(2, 2, 5, September, null, 3)
            .Teacher(3, 2, 5, September, null, 2).TeacherContract(5, "Zeta", "Z", "T5", 1).Build();

        // Act & Assert
        Assert.Equal(["Zeta Z / T5|A1|S1|S2", "Zeta Z / T5|A1|S1|S3", "Zeta Z / T5|B1|S1|S2"],
            Rows(MismatchReports.TeacherSchemeMismatches(snapshot)).Select(Text));
    }

    //the change dates of the report's lines, yyyy-MM-dd
    private static string[] Dates(ReportTable table)
    {
        return
        [
            .. Rows(table).Select(r => ((DateTime)r[2]!).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        ];
    }

    // a change date without an active schedule row has no week hours (Access's vStudenGroupWeekHoursCounts has no
    // row): nothing is compared there, though the student and the teacher are still active
    [Fact]
    public void StudentFeeMismatches_DateWithoutSchedule_IsNotCompared()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, October, 1f)
            .Teacher(1, 1, 5, September, null).Student(1, 1, 20, September, null, 30m).Build();

        // Act & Assert
        Assert.Equal(["2026-09-01"], Dates(MismatchReports.StudentFeeMismatches(snapshot)));
    }

    // a line shows the student's and the teacher's own end dates when they have one
    [Fact]
    public void StudentFeeMismatches_EndedRows_ShowTheirEndDates()
    {
        // Arrange
        DateTime december = Day(2026, 12, 1);
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null, 1f)
            .Teacher(1, 1, 5, September, december).Student(1, 1, 20, September, december.AddDays(5), 30m).Build();

        // Act
        List<object?> line = Assert.Single(Rows(MismatchReports.StudentFeeMismatches(snapshot)));

        // Assert
        Assert.Equal(december.AddDays(5), line[4]);
        Assert.Equal(december, line[6]);
    }

    // the end of another row of the student in the group is a change date: here the right first row ends on
    // October 1st, the wrong second one (from September 15th) goes on
    [Fact]
    public void StudentFeeMismatches_StudentRowEnd_IsAChangeDate()
    {
        // Arrange: 6 × 1 × 1 hour × 4 = 24
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null, 1f)
            .Teacher(1, 1, 5, September, null).Student(1, 1, 20, September, October, 24m)
            .Student(2, 1, 20, Day(2026, 9, 15), null, 30m).Build();

        // Act & Assert
        Assert.Equal(["2026-09-15", "2026-10-01"], Dates(MismatchReports.StudentFeeMismatches(snapshot)));
    }

    // a teacher row's end is a change date: the other teacher row goes on alone
    [Fact]
    public void StudentFeeMismatches_TeacherRowEnd_IsAChangeDate()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null, 1f)
            .Teacher(1, 1, 5, September, October).Teacher(2, 1, 6, September, null)
            .Student(1, 1, 20, September, null, 30m).Build();

        // Act & Assert
        Assert.Equal(["2026-09-01", "2026-09-01", "2026-10-01"], Dates(MismatchReports.StudentFeeMismatches(snapshot)));
    }

    // the group's cancellation is a change date, and the rows are still compared on it (Access)
    [Fact]
    public void StudentFeeMismatches_GroupCancellation_IsAChangeDate()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1", voidDate: October)
            .DayTime(1, 1, September, null, 1f).Teacher(1, 1, 5, September, null)
            .Student(1, 1, 20, September, null, 30m).Build();

        // Act & Assert
        Assert.Equal(["2026-09-01", "2026-10-01"], Dates(MismatchReports.StudentFeeMismatches(snapshot)));
    }

    // an open end makes the academic years' last finish (vMaxDate) a change date; on it a row with a later end of
    // its own is still active and compared. Every kind of open end counts: a student, a schedule or a teacher row,
    // or a group without a cancellation
    [Theory]
    [InlineData("student", "2026-09-01|2027-09-01")]
    [InlineData("schedule", "2026-09-01|2027-09-01")]
    [InlineData("teacher", "2026-09-01|2026-09-01|2027-09-01")]
    [InlineData("group", "2026-09-01|2027-09-01")]
    public void StudentFeeMismatches_OpenEnd_MakesTheLastFinishAChangeDate(string openRow, string expectedDates)
    {
        // Arrange: every row ends after the last finish (2027-09-01) except the open one
        DateTime later = Day(2027, 12, 31);
        GroupRowsSnapshotBuilder builder = new GroupRowsSnapshotBuilder()
            .Group(1, "A1", voidDate: openRow == "group" ? null : later).DayTime(1, 1, September, later, 1f)
            .Teacher(1, 1, 5, September, later).Student(1, 1, 20, September, later, 30m);
        _ = openRow switch
        {
            //a right row of the same student in the group, open
            "student" => builder.Student(2, 1, 20, September, null, 24m),
            "schedule" => builder.DayTime(2, 1, September, null, 1f),
            "teacher" => builder.Teacher(2, 1, 6, September, null),
            _ => builder
        };

        // Act & Assert
        Assert.Equal(expectedDates, string.Join("|", Dates(MismatchReports.StudentFeeMismatches(builder.Build()))));
    }

    // two groups with one code (another year): by the group before the student
    [Fact]
    public void StudentFeeMismatches_GroupsWithOneCode_ByGroup()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(2, "A1").Group(1, "A1")
            .DayTime(1, 1, September, null).DayTime(2, 2, September, null).Teacher(1, 1, 5, September, null)
            .Teacher(2, 2, 5, September, null).Student(1, 2, 21, September, null, 1m)
            .Student(2, 1, 20, September, null, 1m).StudentName(20, "Zeta", "Z", "6.020")
            .StudentName(21, "Alpha", "A", "6.021").Build();

        // Act & Assert
        Assert.Equal(["Zeta", "Alpha"],
            Rows(MismatchReports.StudentFeeMismatches(snapshot)).Select(r => ((string)r[1]!).Split(' ')[0]));
    }

    // two rows of one contract in the group active on one date: by the student row
    [Fact]
    public void StudentFeeMismatches_TwoRowsOfOneStudent_ByStudentRow()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null, 1f)
            .Teacher(1, 1, 5, September, null).Student(2, 1, 20, September, null, 30m)
            .Student(1, 1, 20, September, null, 31m).Build();

        // Act & Assert
        Assert.Equal([31m, 30m], Rows(MismatchReports.StudentFeeMismatches(snapshot)).Select(r => (decimal)r[11]!));
    }

    // the fee must be hour fee × coefficient × the schedule's week hours × 4 on every change date of the student's
    // group: here the schedule loses a row on October 1st, so the right fee of September is wrong from then on
    [Fact]
    public void StudentFeeMismatches_ListsTheChangeDatesWhereTheFeeDiffers()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null)
            .DayTime(2, 1, September, October).Teacher(1, 1, 5, September, null)
            .Student(1, 1, 20, September, null, 72m).Student(2, 1, 21, September, null, 70m).Build();

        // Act
        ReportTable table = MismatchReports.StudentFeeMismatches(snapshot);

        // Assert
        Assert.Equal([
            "groupCode", "student", "changeDate", "studentStartDate", "studentEndDate", "teacherStartDate",
            "teacherEndDate", "fourWeekHours", "oneHourFee", "hoursCoefficient", "weekHours", "fourWeekFee",
            "mustFourWeekFee", "missFourWeekFee"
        ], table.Columns.Select(c => c.Name));
        Assert.Equal([
            "ჯგუფი", "მოსწავლე", "თარიღი", "მოსწავლის დაწყება", "მოსწავლის დასრულება", "მასწავლებლის დაწყება",
            "მასწავლებლის დასრულება", "4 კვირაში საათები", "საათის ღირებულება", "საათის კოეფიციენტი",
            "კვირაში საათები", "4 კვირაში გადასახადი", "უნდა იყოს", "სხვაობა"
        ], table.Columns.Select(c => c.Caption));
        Assert.Equal([ReportColumnTypes.Text, ReportColumnTypes.Text], table.Columns.Take(2).Select(c => c.Type));
        Assert.All(table.Columns.Skip(2).Take(5), c => Assert.Equal(ReportColumnTypes.Date, c.Type));
        Assert.All(table.Columns.Skip(7), c => Assert.Equal(ReportColumnTypes.Number, c.Type));
        Assert.Equal([
            "A1|SLast20 SFirst20 / 6.020|10-01|09-01|09-01|09-01|09-01|8|6|1|1.5|72|36|-36",
            "A1|SLast21 SFirst21 / 6.021|09-01|09-01|09-01|09-01|09-01|8|6|1|3|70|72|2",
            "A1|SLast21 SFirst21 / 6.021|10-01|09-01|09-01|09-01|09-01|8|6|1|1.5|70|36|-34"
        ], Rows(table).Select(Text));
        //the open ends are the academic years' last finish date
        Assert.Equal(MaxFinishDate, Rows(table)[0][4]);
        Assert.Equal(MaxFinishDate, Rows(table)[0][6]);
        //Access's report total =Sum([FourWeekFee]) under the fee column
        Assert.Equal(["სულ:", null, null, null, null, null, null, null, null, null, null, 212m, null, null],
            Assert.Single(table.FooterRows));
    }

    // Access's Abs(…) > 0.01: one hundredth is still right, a bit more is listed; the coefficient counts
    [Theory]
    [InlineData(24.01, false)]
    [InlineData(23.99, false)]
    [InlineData(24.011, true)]
    [InlineData(23.989, true)]
    public void StudentFeeMismatches_ToleratesOneHundredth(double fee, bool listed)
    {
        // Arrange: 6 × 0.5 × 2 hours × 4 = 24
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null, 2f)
            .Teacher(1, 1, 5, September, null).Student(1, 1, 20, September, null, (decimal)fee, 6m, 0.5f).Build();

        // Act & Assert
        Assert.Equal(listed ? 1 : 0, Rows(MismatchReports.StudentFeeMismatches(snapshot)).Count);
    }

    // a line per teacher row active on the date (Access's join); a date without an active schedule or teacher row,
    // or a student row that is not active on it, gives nothing
    [Fact]
    public void StudentFeeMismatches_OneLinePerActiveTeacherRow()
    {
        // Arrange: the second teacher row starts on October 15th, the student row ends on November 1st, the schedule
        // pauses from December 1st and starts again on December 15th
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1")
            .DayTime(1, 1, September, Day(2026, 12, 1), 1f).DayTime(2, 1, Day(2026, 12, 15), null, 1f)
            .Teacher(1, 1, 5, September, null).Teacher(2, 1, 6, Day(2026, 10, 15), null)
            .Student(1, 1, 20, September, Day(2026, 11, 1), 30m).Build();

        // Act
        List<List<object?>> rows = Rows(MismatchReports.StudentFeeMismatches(snapshot));

        // Assert: change dates 09-01 (one teacher) and 10-15 (two teachers); 11-01 and later the student row is over
        Assert.Equal(["09-01|09-01", "10-15|09-01", "10-15|10-15"],
            rows.Select(r => string.Join("|", ((DateTime)r[2]!).ToString("MM-dd", CultureInfo.InvariantCulture),
                ((DateTime)r[5]!).ToString("MM-dd", CultureInfo.InvariantCulture))));
    }

    // the group's cancellation is a change date: after it the open rows are still compared (Access did not check
    // the cancellation), here the fee is right before and after
    [Fact]
    public void StudentFeeMismatches_RightFee_IsNotListed()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1", voidDate: Day(2027, 1, 1))
            .DayTime(1, 1, September, null, 2f).Teacher(1, 1, 5, September, null)
            .Student(1, 1, 20, September, null, 48m).Build();

        // Act
        ReportTable table = MismatchReports.StudentFeeMismatches(snapshot);

        // Assert
        Assert.Empty(Rows(table));
        Assert.Equal(0m, Assert.Single(table.FooterRows)[11]);
    }

    // without academic years an open end is no date (Access's Null): open rows are active on no change date
    [Fact]
    public void StudentFeeMismatches_NoAcademicYears_OpenRowsAreNotCompared()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "A1").DayTime(1, 1, September, null)
            .Teacher(1, 1, 5, September, null).Student(1, 1, 20, September, null, 1m).MaxFinishDate(null).Build();

        // Act & Assert
        Assert.Empty(Rows(MismatchReports.StudentFeeMismatches(snapshot)));
    }

    // order: group code, student, change date
    [Fact]
    public void StudentFeeMismatches_OrdersByGroupStudentAndDate()
    {
        // Arrange
        GroupRowsSnapshot snapshot = new GroupRowsSnapshotBuilder().Group(1, "B1").Group(2, "A1")
            .DayTime(1, 1, September, null).DayTime(2, 2, September, null).DayTime(3, 2, October, null)
            .Teacher(1, 1, 5, September, null).Teacher(2, 2, 5, September, null)
            .Student(1, 1, 20, September, null, 1m).Student(2, 2, 21, September, null, 1m)
            .Student(3, 2, 20, September, null, 1m).StudentName(20, "Zeta", "Z", "6.020")
            .StudentName(21, "Alpha", "A", "6.021").Build();

        // Act & Assert
        Assert.Equal(["A1 Alpha 09-01", "A1 Alpha 10-01", "A1 Zeta 09-01", "A1 Zeta 10-01", "B1 Zeta 09-01"],
            Rows(MismatchReports.StudentFeeMismatches(snapshot)).Select(r =>
                $"{r[0]} {((string)r[1]!).Split(' ')[0]} {((DateTime)r[2]!).ToString("MM-dd", CultureInfo.InvariantCulture)}"));
    }
}
