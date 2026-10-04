using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.ScheduleSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ScheduleOverlapReportsTests
{
    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static void AssertRow(List<object?> actual, params object?[] expected)
    {
        Assert.Equal(expected, actual);
    }

    private static void AssertGroups(ReportTable table, params string[] groupCodes)
    {
        Assert.Equal(groupCodes, Rows(table).Select(r => (string)r[4]!));
    }

    // two lessons in one room on one week day whose [start, start + hours) intersect
    [Fact]
    public void RoomOverlaps_IntersectingLessons_ListsBoth()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "11:00", 1f).Build();

        // Act
        ReportTable table = ScheduleOverlapReports.RoomOverlaps(schedule);

        // Assert
        Assert.Equal(["roomName", "weekDay", "startTime", "endTime", "groupCode"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ოთახი", "კვირის დღე", "დრო", "დასრულება", "ჯგუფი"], table.Columns.Select(c => c.Caption));
        Assert.Equal([
            ReportColumnTypes.Text, ReportColumnTypes.Text, ReportColumnTypes.Time, ReportColumnTypes.Time,
            ReportColumnTypes.Text
        ], table.Columns.Select(c => c.Type));
        List<List<object?>> rows = Rows(table);
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], "R1", "1-ორ", Time("10:00"), Time("11:30"), "A1");
        AssertRow(rows[1], "R1", "1-ორ", Time("11:00"), Time("12:00"), "B1");
    }

    // Access's case: the same start time
    [Fact]
    public void RoomOverlaps_SameStartTime_Overlaps()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 3, "15:00")
            .Lesson(2, 2, 3, "15:00").Build();

        // Act & Assert
        AssertGroups(ScheduleOverlapReports.RoomOverlaps(schedule), "A1", "B1");
    }

    // a lesson that starts when the other ends does not overlap it
    [Fact]
    public void RoomOverlaps_BackToBackLessons_DoNotOverlap()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "11:30", 2f).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.RoomOverlaps(schedule)));
    }

    [Fact]
    public void RoomOverlaps_OtherWeekDayOrRoom_DoesNotOverlap()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Group(3, "C1")
            .Lesson(1, 1, 1, "10:00").Lesson(2, 2, 2, "10:00").Lesson(3, 3, 1, "10:00", roomId: 2).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.RoomOverlaps(schedule)));
    }

    // every lesson of a chain is listed once, also the one that overlaps two others
    [Fact]
    public void RoomOverlaps_Chain_ListsEveryLessonOnce()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Group(3, "C1")
            .Group(4, "D1").Lesson(1, 1, 1, "10:00", 1f).Lesson(2, 2, 1, "10:30", 1f).Lesson(3, 3, 1, "11:15", 1f)
            .Lesson(4, 4, 1, "13:00", 1f).Build();

        // Act & Assert
        AssertGroups(ScheduleOverlapReports.RoomOverlaps(schedule), "A1", "B1", "C1");
    }

    // order: room, week day (in the schedule's week day order), start time, group code
    [Fact]
    public void RoomOverlaps_OrdersByRoomWeekDayTimeAndGroup()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder()
            .WeekDays(new ScheduleWeekDay(2, "Tue"), new ScheduleWeekDay(1, "Mon")).Room(2, "A-room").Group(1, "G2")
            .Group(2, "G1").Group(3, "G3").Group(4, "G4").Group(5, "G5").Group(6, "G6").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "10:00").Lesson(3, 3, 2, "12:00").Lesson(4, 4, 2, "11:00")
            .Lesson(5, 5, 1, "09:00", roomId: 2).Lesson(6, 6, 1, "09:30", roomId: 2).Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleOverlapReports.RoomOverlaps(schedule));

        // Assert
        Assert.Equal(["A-room|Mon|G5", "A-room|Mon|G6", "R1|Tue|G4", "R1|Tue|G3", "R1|Mon|G1", "R1|Mon|G2"],
            rows.Select(r => $"{r[0]}|{r[1]}|{r[4]}"));
    }

    // two rooms with one name: each room's lessons stay together, the room with the smaller id first
    [Fact]
    public void RoomOverlaps_TwoRoomsWithOneName_KeepsEachRoomsLessonsTogether()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Room(1, "R").Room(2, "R").Group(1, "A1")
            .Group(2, "B1").Group(3, "C1").Group(4, "D1").Lesson(1, 1, 1, "09:00", roomId: 2)
            .Lesson(2, 2, 1, "09:30", roomId: 2).Lesson(3, 3, 1, "10:00").Lesson(4, 4, 1, "10:30").Build();

        // Act & Assert
        AssertGroups(ScheduleOverlapReports.RoomOverlaps(schedule), "C1", "D1", "A1", "B1");
    }

    // one group's two schedule rows at one time: in the order of the schedule rows
    [Fact]
    public void RoomOverlaps_GroupTwiceAtOneTime_KeepsTheScheduleRowsOrder()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Lesson(5, 1, 1, "10:00", 2f)
            .Lesson(6, 1, 1, "10:00", 1f).Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleOverlapReports.RoomOverlaps(schedule));

        // Assert
        Assert.Equal([Time("12:00"), Time("11:00")], rows.Select(r => r[3]));
    }

    // a teacher teaching two groups at overlapping times; "last first / number"
    [Fact]
    public void TeacherOverlaps_TeacherInTwoGroups_ListsBothLessons()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Group(3, "C1")
            .Lesson(1, 1, 4, "14:30", 2f).Lesson(2, 2, 4, "16:00", 2f, 2).Lesson(3, 3, 4, "14:30", 2f).Teacher(1, 1, 5)
            .Teacher(2, 2, 5).Teacher(3, 3, 6).TeacherName(5, "Alpha", "Ann", "T3.05").Build();

        // Act
        ReportTable table = ScheduleOverlapReports.TeacherOverlaps(schedule);

        // Assert
        Assert.Equal(["teacher", "weekDay", "startTime", "endTime", "groupCode"], table.Columns.Select(c => c.Name));
        Assert.Equal("მასწავლებელი", table.Columns[0].Caption);
        List<List<object?>> rows = Rows(table);
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], "Alpha Ann / T3.05", "4-ხუთ", Time("14:30"), Time("16:30"), "A1");
        AssertRow(rows[1], "Alpha Ann / T3.05", "4-ხუთ", Time("16:00"), Time("18:00"), "B1");
    }

    // a teacher registered twice in one group teaches one lesson, not two overlapping ones (that is r21's case)
    [Fact]
    public void TeacherOverlaps_TeacherTwiceInOneGroup_IsNotAnOverlap()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Lesson(1, 1, 1, "10:00")
            .Teacher(1, 1, 5).Teacher(2, 1, 5).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.TeacherOverlaps(schedule)));
    }

    // the owner is the contract: two contracts with the same name are two teachers
    [Fact]
    public void TeacherOverlaps_TwoContractsWithTheSameName_AreDifferentTeachers()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "10:00").Teacher(1, 1, 5).Teacher(2, 2, 6).TeacherName(5, "Same", "Name", "T1")
            .TeacherName(6, "Same", "Name", "T1").Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.TeacherOverlaps(schedule)));
    }

    // two teachers' lessons in the same room do not matter here
    [Fact]
    public void TeacherOverlaps_DifferentTeachers_DoNotOverlap()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "10:00").Teacher(1, 1, 5).Teacher(2, 2, 6).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.TeacherOverlaps(schedule)));
    }

    // order: teacher name, then the contract, week day, time and group
    [Fact]
    public void TeacherOverlaps_OrdersByTeacherWeekDayTimeAndGroup()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Group(3, "C1")
            .Group(4, "D1").Lesson(1, 1, 2, "10:00").Lesson(2, 2, 2, "10:30").Lesson(3, 3, 1, "12:00")
            .Lesson(4, 4, 1, "12:00").Teacher(1, 1, 5).Teacher(2, 2, 5).Teacher(3, 3, 6).Teacher(4, 4, 6)
            .TeacherName(5, "Beta", "B", "T5").TeacherName(6, "Alpha", "A", "T6").Build();

        // Act & Assert
        AssertGroups(ScheduleOverlapReports.TeacherOverlaps(schedule), "C1", "D1", "A1", "B1");
    }

    [Fact]
    public void StudentOverlaps_StudentInTwoGroups_ListsBothLessons()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "B1").Lesson(1, 1, 5, "17:00")
            .Lesson(2, 2, 5, "18:00", 1.5f, 2).Student(1, 1, 20).Student(2, 2, 20).Student(3, 2, 21)
            .StudentName(20, "Gamma", "Gia", "6.020").Build();

        // Act
        ReportTable table = ScheduleOverlapReports.StudentOverlaps(schedule);

        // Assert
        Assert.Equal(["student", "weekDay", "startTime", "endTime", "groupCode"], table.Columns.Select(c => c.Name));
        Assert.Equal("მოსწავლე", table.Columns[0].Caption);
        List<List<object?>> rows = Rows(table);
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], "Gamma Gia / 6.020", "5-პარ", Time("17:00"), Time("18:30"), "A1");
        AssertRow(rows[1], "Gamma Gia / 6.020", "5-პარ", Time("18:00"), Time("19:30"), "B1");
    }

    [Fact]
    public void StudentOverlaps_StudentTwiceInOneGroup_IsNotAnOverlap()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Lesson(1, 1, 1, "10:00")
            .Student(1, 1, 20).Student(2, 1, 20).Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleOverlapReports.StudentOverlaps(schedule)));
    }

    // a lesson past midnight: the end is compared beyond 24 hours, shown from the day's start
    [Fact]
    public void FindOverlapping_LessonPastMidnight_ComparesTheFullInterval()
    {
        // Arrange
        ScheduleLessonRow late = new(1, 1, 1, Time("23:00"), 2f, 1);
        ScheduleLessonRow later = new(2, 2, 1, Time("23:30"), 1f, 1);

        // Act
        List<ScheduleOccupancy> overlapping = ScheduleOverlapReports.FindOverlapping([
            new ScheduleOccupancy(1, "R1", late), new ScheduleOccupancy(1, "R1", later)
        ]);

        // Assert
        Assert.Equal(2, overlapping.Count);
        Assert.Equal(Time("01:00"), late.EndTime);
    }

    // the same lesson twice for one owner (two registrations) is one lesson
    [Fact]
    public void FindOverlapping_SameLessonTwice_IsListedOnceIfItOverlapsAnother()
    {
        // Arrange
        ScheduleLessonRow first = new(1, 1, 1, Time("10:00"), 1f, 1);
        ScheduleLessonRow second = new(2, 2, 1, Time("10:30"), 1f, 1);

        // Act
        List<ScheduleOccupancy> overlapping = ScheduleOverlapReports.FindOverlapping([
            new ScheduleOccupancy(5, "T", first), new ScheduleOccupancy(5, "T", first),
            new ScheduleOccupancy(5, "T", second)
        ]);

        // Assert
        Assert.Equal([1, 2], overlapping.Select(o => o.Lesson.GdtpId).Order());
    }
}
