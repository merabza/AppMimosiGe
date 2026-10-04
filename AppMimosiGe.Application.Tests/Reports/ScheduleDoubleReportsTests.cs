using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ScheduleDoubleReportsTests
{
    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static void AssertRows(ReportTable table, params string[] expected)
    {
        Assert.Equal(expected, Rows(table).Select(r => string.Join("|", r)));
    }

    // a student in two groups of one course, or twice in one group
    [Fact]
    public void StudentDoubleCourses_TwoRowsOfOneCourse_ListTheStudentAndCourse()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 3).Group(2, "B1", 3)
            .Group(3, "C1", 4, "Art").Student(1, 1, 20).Student(2, 2, 20).Student(3, 3, 20).Student(4, 3, 21)
            .Student(5, 3, 21).Student(6, 1, 22).StudentName(20, "Alpha", "Ann", "6.020")
            .StudentName(21, "Beta", "Bob", "6.021").Build();

        // Act
        ReportTable table = ScheduleDoubleReports.StudentDoubleCourses(schedule);

        // Assert
        Assert.Equal(["student", "courseName"], table.Columns.Select(c => c.Name));
        Assert.Equal(["მოსწავლე", "საგანი"], table.Columns.Select(c => c.Caption));
        Assert.All(table.Columns, c => Assert.Equal(ReportColumnTypes.Text, c.Type));
        AssertRows(table, "Alpha Ann / 6.020|Math", "Beta Bob / 6.021|Art");
    }

    // the course is compared by its id: two courses with one name are two courses
    [Fact]
    public void StudentDoubleCourses_TwoCoursesWithOneName_AreNotDoubles()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 3).Group(2, "B1", 4).Student(1, 1, 20)
            .Student(2, 2, 20).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleDoubleReports.StudentDoubleCourses(schedule)));
    }

    // order: student "last first / number", then course
    [Fact]
    public void StudentDoubleCourses_OrdersByStudentAndCourse()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 3).Group(2, "A2", 3)
            .Group(3, "B1", 4, "Art").Group(4, "B2", 4, "Art").Student(1, 1, 20).Student(2, 2, 20).Student(3, 3, 20)
            .Student(4, 4, 20).Student(5, 1, 21).Student(6, 2, 21).StudentName(20, "Zeta", "Z", "6.020")
            .StudentName(21, "Alpha", "A", "6.021").Build();

        // Act & Assert
        AssertRows(ScheduleDoubleReports.StudentDoubleCourses(schedule), "Alpha A / 6.021|Math", "Zeta Z / 6.020|Art",
            "Zeta Z / 6.020|Math");
    }

    [Fact]
    public void TeacherDoubleGroups_TeacherTwiceInOneGroup_ListsTheTeacherAndGroup()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Teacher(1, 1, 5)
            .Teacher(2, 1, 5).Teacher(3, 2, 5).Teacher(4, 2, 6).TeacherName(5, "Alpha", "Ann", "T3.05").Build();

        // Act
        ReportTable table = ScheduleDoubleReports.TeacherDoubleGroups(schedule);

        // Assert
        Assert.Equal(["teacher", "groupCode"], table.Columns.Select(c => c.Name));
        Assert.Equal(["მასწავლებელი", "ჯგუფი"], table.Columns.Select(c => c.Caption));
        Assert.All(table.Columns, c => Assert.Equal(ReportColumnTypes.Text, c.Type));
        AssertRows(table, "Alpha Ann / T3.05|A1");
    }

    // order: teacher "last first / number", then group code
    [Fact]
    public void TeacherDoubleGroups_OrdersByTeacherAndGroup()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "B1").Group(2, "A1").Group(3, "C1")
            .Teacher(1, 1, 5).Teacher(2, 1, 5).Teacher(3, 2, 5).Teacher(4, 2, 5).Teacher(5, 3, 6).Teacher(6, 3, 6)
            .TeacherName(5, "Zeta", "Z", "T5").TeacherName(6, "Alpha", "A", "T6").Build();

        // Act & Assert
        AssertRows(ScheduleDoubleReports.TeacherDoubleGroups(schedule), "Alpha A / T6|C1", "Zeta Z / T5|A1",
            "Zeta Z / T5|B1");
    }
}
