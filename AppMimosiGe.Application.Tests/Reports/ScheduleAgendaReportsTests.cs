using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.ScheduleSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ScheduleAgendaReportsTests
{
    private static readonly string[] WeekDayCaptions = ["1-ორ", "2-სამ", "3-ოთხ", "4-ხუთ", "5-პარ", "6-შაბ", "7-კვ"];

    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static void AssertRow(List<object?> actual, params object?[] expected)
    {
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RoomsAgenda_PutsTheStartTimesIntoTheWeekDayColumns()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(10, "A1", 3, "English")
            .Lesson(1, 10, 1, "10:00").Lesson(2, 10, 3, "11:30").Teacher(1, 10, 5).Build();

        // Act
        ReportTable table = ScheduleAgendaReports.RoomsAgenda(schedule);

        // Assert
        Assert.Equal(["roomName", "lastName", "firstName", "courseName", "groupCode", .. WeekDayNames()],
            table.Columns.Select(c => c.Name));
        Assert.Equal(["ოთახი", "გვარი", "სახელი", "საგანი", "ჯგუფი", .. WeekDayCaptions],
            table.Columns.Select(c => c.Caption));
        Assert.Equal([
            .. Enumerable.Repeat(ReportColumnTypes.Text, 5), .. Enumerable.Repeat(ReportColumnTypes.Time, 7)
        ], table.Columns.Select(c => c.Type));
        AssertRow(Assert.Single(Rows(table)), "R1", "TLast5", "TFirst5", "English", "A1", Time("10:00"), null,
            Time("11:30"), null, null, null, null);
        Assert.Null(Assert.Single(table.Sections).Header);
        Assert.Empty(table.FooterRows);
    }

    // Access took First() of the times; the earliest one is shown here
    [Fact]
    public void RoomsAgenda_TwoTimesInOneCell_ShowsTheEarliest()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(10, "A1").Lesson(1, 10, 2, "15:00")
            .Lesson(2, 10, 2, "09:30").Lesson(3, 10, 2, "12:00").Teacher(1, 10, 5).Build();

        // Act
        List<object?> row = Assert.Single(Rows(ScheduleAgendaReports.RoomsAgenda(schedule)));

        // Assert
        Assert.Equal(Time("09:30"), row[6]);
    }

    // the room is part of the row key, as in Access's GROUP BY
    [Fact]
    public void RoomsAgenda_GroupInTwoRooms_HasARowPerRoom()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(10, "A1").Lesson(1, 10, 1, "10:00", roomId: 2)
            .Lesson(2, 10, 4, "10:00").Teacher(1, 10, 5).Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleAgendaReports.RoomsAgenda(schedule));

        // Assert
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], "R1", "TLast5", "TFirst5", "Math", "A1", null, null, null, Time("10:00"), null, null, null);
        AssertRow(rows[1], "R2", "TLast5", "TFirst5", "Math", "A1", Time("10:00"), null, null, null, null, null, null);
    }

    // Access's crosstab order: room, teacher's last and first name, course, group
    [Fact]
    public void RoomsAgenda_OrdersByRoomTeacherCourseAndGroup()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "B2").Group(2, "B1").Group(3, "C1", 2, "Art")
            .Group(4, "D1").Group(5, "E1").Lesson(1, 1, 1, "10:00").Lesson(2, 2, 1, "11:00").Lesson(3, 3, 1, "12:00")
            .Lesson(4, 4, 1, "13:00").Lesson(5, 5, 1, "14:00", 1, 2).Teacher(1, 1, 7).Teacher(2, 2, 7).Teacher(3, 3, 7)
            .Teacher(4, 4, 8).Teacher(5, 5, 7).TeacherName(7, "Beta", "Ann", "T7").TeacherName(8, "Alpha", "Zed", "T8")
            .Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleAgendaReports.RoomsAgenda(schedule));

        // Assert
        Assert.Equal(["D1", "C1", "B1", "B2", "E1"], rows.Select(r => r[4]));
    }

    [Fact]
    public void RoomsAgenda_FirstNameBreaksTheTieOfTheLastName()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "A2").Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "11:00").Teacher(1, 1, 7).Teacher(2, 2, 8).TeacherName(7, "Same", "Bob", "T7")
            .TeacherName(8, "Same", "Ann", "T8").Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleAgendaReports.RoomsAgenda(schedule));

        // Assert
        Assert.Equal(["A2", "A1"], rows.Select(r => r[4]));
    }

    // a group without a teacher row has no row (Access's INNER JOIN GroupsByTeachers)
    [Fact]
    public void RoomsAgenda_LessonWithoutTeacher_HasNoRow()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Lesson(1, 1, 1, "10:00").Build();

        // Act & Assert
        Assert.Empty(Rows(ScheduleAgendaReports.RoomsAgenda(schedule)));
    }

    // the week day columns follow the order of the schedule's week days, always all of them
    [Fact]
    public void RoomsAgenda_WeekDayColumnsFollowTheWeekDays()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder()
            .WeekDays(new ScheduleWeekDay(7, "Sun"), new ScheduleWeekDay(1, "Mon")).Group(1, "A1")
            .Lesson(1, 1, 1, "10:00").Teacher(1, 1, 5).Build();

        // Act
        ReportTable table = ScheduleAgendaReports.RoomsAgenda(schedule);

        // Assert
        Assert.Equal(["weekDay7", "weekDay1"], table.Columns.Skip(5).Select(c => c.Name));
        Assert.Equal(["Sun", "Mon"], table.Columns.Skip(5).Select(c => c.Caption));
        AssertRow(Assert.Single(Rows(table)), "R1", "TLast5", "TFirst5", "Math", "A1", null, Time("10:00"));
    }

    [Fact]
    public void RoomsAgenda_NoLessons_HasTheColumnsAndNoRows()
    {
        // Act
        ReportTable table = ScheduleAgendaReports.RoomsAgenda(new ScheduleSnapshotBuilder().Build());

        // Assert
        Assert.Equal(12, table.Columns.Count);
        Assert.Empty(Rows(table));
    }

    // a row per student, group, course and teacher; names are "last first", without the contract number
    [Fact]
    public void StudentsAgenda_HasARowPerStudentGroupAndTeacher()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 2, "Art").Group(2, "B1", 3)
            .Lesson(1, 1, 1, "10:00").Lesson(2, 1, 5, "12:00").Lesson(3, 2, 2, "09:00").Teacher(1, 1, 5)
            .Teacher(2, 2, 6).Student(1, 1, 20).Student(2, 2, 20).Student(3, 1, 21)
            .StudentName(20, "Alpha", "Ann", "6.020").StudentName(21, "Beta", "Bob", "6.021").Build();

        // Act
        ReportTable table = ScheduleAgendaReports.StudentsAgenda(schedule);

        // Assert
        Assert.Equal(["studentName", "courseName", "groupCode", "teacherName", .. WeekDayNames()],
            table.Columns.Select(c => c.Name));
        Assert.Equal(["მოსწავლე", "საგანი", "ჯგუფი", "მასწავლებელი", .. WeekDayCaptions],
            table.Columns.Select(c => c.Caption));
        List<List<object?>> rows = Rows(table);
        Assert.Equal(3, rows.Count);
        AssertRow(rows[0], "Alpha Ann", "Art", "A1", "TLast5 TFirst5", Time("10:00"), null, null, null, Time("12:00"),
            null, null);
        AssertRow(rows[1], "Alpha Ann", "Math", "B1", "TLast6 TFirst6", null, Time("09:00"), null, null, null, null,
            null);
        AssertRow(rows[2], "Beta Bob", "Art", "A1", "TLast5 TFirst5", Time("10:00"), null, null, null, Time("12:00"),
            null, null);
    }

    // Access's crosstab order: student, group, course, teacher
    [Fact]
    public void StudentsAgenda_OrdersByStudentGroupCourseAndTeacher()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 2, "Zoo").Group(2, "A1", 3, "Art")
            .Group(3, "A0", 4).Lesson(1, 1, 1, "10:00").Lesson(2, 2, 1, "11:00").Lesson(3, 3, 1, "12:00")
            .Teacher(1, 1, 5).Teacher(2, 2, 5).Teacher(3, 2, 4).Teacher(4, 3, 5).Student(1, 1, 20).Student(2, 2, 20)
            .Student(3, 3, 20).TeacherName(4, "Zulu", "Z", "T4").TeacherName(5, "Alpha", "A", "T5").Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleAgendaReports.StudentsAgenda(schedule));

        // Assert
        Assert.Equal(["A0|Math|Alpha A", "A1|Art|Alpha A", "A1|Art|Zulu Z", "A1|Zoo|Alpha A"],
            rows.Select(r => $"{r[2]}|{r[1]}|{r[3]}"));
    }

    [Fact]
    public void TeachersAgenda_HasARowPerTeacherCourseGroupAndRoom()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1", 2, "Art").Lesson(1, 1, 6, "10:00")
            .Lesson(2, 1, 7, "11:00", roomId: 2).Teacher(1, 1, 5).Build();

        // Act
        ReportTable table = ScheduleAgendaReports.TeachersAgenda(schedule);

        // Assert
        Assert.Equal(["lastName", "firstName", "courseName", "groupCode", "roomName", .. WeekDayNames()],
            table.Columns.Select(c => c.Name));
        Assert.Equal(["გვარი", "სახელი", "საგანი", "ჯგუფი", "ოთახი", .. WeekDayCaptions],
            table.Columns.Select(c => c.Caption));
        List<List<object?>> rows = Rows(table);
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], "TLast5", "TFirst5", "Art", "A1", "R1", null, null, null, null, null, Time("10:00"), null);
        AssertRow(rows[1], "TLast5", "TFirst5", "Art", "A1", "R2", null, null, null, null, null, null, Time("11:00"));
    }

    // Access's crosstab order: last name, first name, course, group, room
    [Fact]
    public void TeachersAgenda_OrdersByTeacherCourseGroupAndRoom()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Room(3, "R0").Group(1, "B1", 2, "Art")
            .Group(2, "A1", 2, "Art").Group(3, "C1").Group(4, "D1").Lesson(1, 1, 1, "10:00").Lesson(2, 2, 1, "11:00")
            .Lesson(3, 3, 1, "12:00").Lesson(4, 2, 2, "11:00", roomId: 3).Lesson(5, 4, 1, "12:00")
            .Group(5, "Z1", 3, "Zoo").Lesson(6, 5, 1, "13:00").Teacher(1, 1, 5).Teacher(2, 2, 5).Teacher(3, 3, 5)
            .Teacher(4, 4, 6).Teacher(5, 5, 7).TeacherName(5, "Same", "Bea", "T5").TeacherName(6, "Same", "Ada", "T6")
            .TeacherName(7, "Alpha", "Zed", "T7").Build();

        // Act
        List<List<object?>> rows = Rows(ScheduleAgendaReports.TeachersAgenda(schedule));

        // Assert
        Assert.Equal(["Z1|R1", "D1|R1", "A1|R0", "A1|R1", "B1|R1", "C1|R1"], rows.Select(r => $"{r[3]}|{r[4]}"));
    }

    // the count of lessons starting at a time on a week day; no lesson leaves the cell empty
    [Fact]
    public void UsedDayTimes_CountsTheLessonsPerStartTimeAndWeekDay()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Group(2, "A2").Lesson(1, 1, 1, "15:00")
            .Lesson(2, 2, 1, "15:00").Lesson(3, 1, 3, "15:00").Lesson(4, 2, 1, "10:00").Lesson(5, 1, 7, "10:00")
            .Build();

        // Act
        ReportTable table = ScheduleAgendaReports.UsedDayTimes(schedule);

        // Assert
        Assert.Equal(["startTime", .. WeekDayNames()], table.Columns.Select(c => c.Name));
        Assert.Equal(["დრო", .. WeekDayCaptions], table.Columns.Select(c => c.Caption));
        Assert.Equal([ReportColumnTypes.Time, .. Enumerable.Repeat(ReportColumnTypes.WholeNumber, 7)],
            table.Columns.Select(c => c.Type));
        List<List<object?>> rows = Rows(table);
        Assert.Equal(2, rows.Count);
        AssertRow(rows[0], Time("10:00"), 1, null, null, null, null, null, 1);
        AssertRow(rows[1], Time("15:00"), 2, null, 1, null, null, null, null);
    }

    // Access's report footer: the sum of every day (0 for a day without lessons) and the total
    [Fact]
    public void UsedDayTimes_FooterHasTheDaySumsAndTheTotal()
    {
        // Arrange
        ScheduleSnapshot schedule = new ScheduleSnapshotBuilder().Group(1, "A1").Lesson(1, 1, 1, "15:00")
            .Lesson(2, 1, 1, "10:00").Lesson(3, 1, 3, "15:00").Build();

        // Act
        ReportTable table = ScheduleAgendaReports.UsedDayTimes(schedule);

        // Assert
        Assert.Equal(2, table.FooterRows.Count);
        AssertRow(table.FooterRows[0], "დღეების ჯამები:", 2, 0, 1, 0, 0, 0, 0);
        AssertRow(table.FooterRows[1], "სულ:", 3, null, null, null, null, null, null);
    }

    private static IEnumerable<string> WeekDayNames()
    {
        return Enumerable.Range(1, 7).Select(id => $"weekDay{id}");
    }
}
