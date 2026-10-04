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

public sealed class LessonCheckReportsTests
{
    private const int NotCancelled = 1;
    private const int Cancelled = 2;
    private const int CancelledByOthers = 3;

    private static readonly SchedulePerson TeacherA = new("Alpha", "Ann", "T5");
    private static readonly SchedulePerson TeacherZ = new("Zeta", "Zoe", "T6");

    private static DateTime At(int day, int hour)
    {
        return new DateTime(2026, 9, day, hour, 0, 0, DateTimeKind.Unspecified);
    }

    //a lesson of teacher A in group G1 unless said otherwise; synthetic people only
    private static PeriodLessonRow Lesson(int id, DateTime lessonDt, int statusId = NotCancelled,
        bool present = false, bool recovered = false, SchedulePerson? substitute = null, string groupCode = "G1",
        SchedulePerson? teacher = null)
    {
        SchedulePerson lessonTeacher = teacher ?? TeacherA;
        return new PeriodLessonRow(id, lessonDt, groupCode, lessonTeacher == TeacherA ? 5 : 6, lessonTeacher, substitute,
            statusId, $"status{statusId}", recovered, present);
    }

    private static List<List<object?>> Rows(ReportTable table)
    {
        return Assert.Single(table.Sections).Rows;
    }

    private static string Text(List<object?> row)
    {
        return string.Join("|", row.Select(c => c switch
        {
            DateTime d => d.ToString("dd HH", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => c
        }));
    }

    // r11: "not cancelled" and nobody present (a lesson without students too); cancelled lessons and lessons with a
    // present student are not listed. Order: the lesson time
    [Fact]
    public void WrongStatusLessons_NotCancelledWithoutPresentStudents()
    {
        // Arrange
        List<PeriodLessonRow> lessons =
        [
            Lesson(1, At(3, 15)), Lesson(2, At(2, 10), present: true), Lesson(3, At(2, 9), Cancelled),
            Lesson(4, At(2, 11), CancelledByOthers), Lesson(5, At(1, 18), groupCode: "G2", teacher: TeacherZ),
            Lesson(6, At(1, 18))
        ];

        // Act
        ReportTable table = LessonCheckReports.WrongStatusLessons(lessons);

        // Assert
        Assert.Equal(["lessonDt", "groupCode", "teacher"], table.Columns.Select(c => c.Name));
        Assert.Equal(["თარიღი და დრო", "ჯგუფი", "მასწავლებელი"], table.Columns.Select(c => c.Caption));
        Assert.Equal([ReportColumnTypes.DateTime, ReportColumnTypes.Text, ReportColumnTypes.Text],
            table.Columns.Select(c => c.Type));
        Assert.Equal(["01 18|G1|Alpha Ann / T5", "01 18|G2|Zeta Zoe / T6", "03 15|G1|Alpha Ann / T5"],
            Rows(table).Select(Text));
        Assert.Empty(table.FooterRows);
    }

    // r12: cancelled (2 or 3) with a present student or a recovery date
    [Fact]
    public void LessonsWithWrongVoidStatus_CancelledWithPresenceOrRecovery()
    {
        // Arrange
        List<PeriodLessonRow> lessons =
        [
            Lesson(1, At(1, 10), Cancelled, true), Lesson(2, At(2, 10), CancelledByOthers, recovered: true),
            Lesson(3, At(3, 10), Cancelled), Lesson(4, At(4, 10), present: true, recovered: true),
            Lesson(5, At(5, 10), CancelledByOthers, true)
        ];

        // Act
        ReportTable table = LessonCheckReports.LessonsWithWrongVoidStatus(lessons);

        // Assert
        Assert.Equal(["lessonDt", "groupCode", "teacher"], table.Columns.Select(c => c.Name));
        Assert.Equal(["01 10|G1|Alpha Ann / T5", "02 10|G1|Alpha Ann / T5", "05 10|G1|Alpha Ann / T5"],
            Rows(table).Select(Text));
    }

    // r13: order by group code, then the lesson time (Access's report grouping)
    [Fact]
    public void LessonsWithErrors_OrdersByGroupAndLessonTime()
    {
        // Arrange
        List<LessonErrorRow> errors =
        [
            new(1, "G2", At(1, 10), "error 11"), new(2, "G1", At(3, 10), "error 14"),
            new(3, "G1", At(2, 10), "error 11")
        ];

        // Act
        ReportTable table = LessonCheckReports.LessonsWithErrors(errors);

        // Assert
        Assert.Equal(["groupCode", "lessonDt", "errorText"], table.Columns.Select(c => c.Name));
        Assert.Equal(["ჯგუფი", "გაკვეთილის თარიღი", "შეცდომის ტექსტი"], table.Columns.Select(c => c.Caption));
        Assert.Equal(ReportColumnTypes.DateTime, table.Columns[1].Type);
        Assert.Equal(["G1|02 10|error 11", "G1|03 10|error 14", "G2|01 10|error 11"], Rows(table).Select(Text));
    }

    // r22: the lessons the repository found (a theoretical date at 00:00:00), by group and time, with the count
    [Fact]
    public void WrongWeekDayChanges_ListsTheLessonsWithTheirCount()
    {
        // Arrange
        List<TeoDatesLessonRow> lessons =
        [
            new(1, "G2", TeacherZ, At(1, 0), At(29, 15), 8f, At(3, 15)),
            new(2, "G1", TeacherA, At(2, 15), At(1, 0), 4.5f, At(9, 15))
        ];

        // Act
        ReportTable table = LessonCheckReports.WrongWeekDayChanges(lessons);

        // Assert
        Assert.Equal(["groupCode", "teacher", "teoMinDate", "teoMaxDate", "fourWeekHours", "lessonDt"],
            table.Columns.Select(c => c.Name));
        Assert.Equal([
            "ჯგუფი", "მასწავლებელი", "თეორიულად მინიმალური თარიღი", "თეორიულად მაქსიმალური თარიღი",
            "4 კვირაში საათები", "ჩატარების თარიღი და დრო"
        ], table.Columns.Select(c => c.Caption));
        Assert.Equal([
            ReportColumnTypes.Text, ReportColumnTypes.Text, ReportColumnTypes.DateTime, ReportColumnTypes.DateTime,
            ReportColumnTypes.Number, ReportColumnTypes.DateTime
        ], table.Columns.Select(c => c.Type));
        Assert.Equal(["G1|Alpha Ann / T5|02 15|01 00|4.5|09 15", "G2|Zeta Zoe / T6|01 00|29 15|8|03 15"],
            Rows(table).Select(Text));
        Assert.Equal(["სულ:", 2, null, null, null, null], Assert.Single(table.FooterRows));
    }

    // r34: cancelled ("გაუქმდა") or substituted lessons, a section per lesson teacher (by name) with the count and the
    // share of all cases; status 3 without a substitute is not a case
    [Fact]
    public void TeacherMissAndSubstitutes_SectionsPerTeacherWithShares()
    {
        // Arrange
        var substitute = new SchedulePerson("Beta", "Bob", "T7");
        List<PeriodLessonRow> lessons =
        [
            Lesson(1, At(3, 10), Cancelled, teacher: TeacherZ), Lesson(2, At(1, 10), substitute: substitute),
            Lesson(3, At(2, 10), CancelledByOthers), Lesson(4, At(4, 10), Cancelled, teacher: TeacherZ),
            Lesson(5, At(5, 10), CancelledByOthers, substitute: substitute, teacher: TeacherZ),
            Lesson(6, At(6, 10))
        ];

        // Act
        ReportTable table = LessonCheckReports.TeacherMissAndSubstitutes(lessons);

        // Assert
        Assert.Equal(["lessonDt", "substituteTeacher", "lessonStatus"], table.Columns.Select(c => c.Name));
        Assert.Equal(["თარიღი", "ჩამნაცვლებელი", "სტატუსი"], table.Columns.Select(c => c.Caption));
        Assert.Equal(ReportColumnTypes.DateTime, table.Columns[0].Type);
        Assert.Equal(["მასწავლებელი: Alpha Ann / T5", "მასწავლებელი: Zeta Zoe / T6"],
            table.Sections.Select(s => s.Header));
        Assert.Equal(["01 10|Beta Bob / T7|status1"], table.Sections[0].Rows.Select(Text));
        Assert.Equal(["03 10||status2", "04 10||status2", "05 10|Beta Bob / T7|status3"],
            table.Sections[1].Rows.Select(Text));
        Assert.Equal(["შემთხვევების რაოდენობა:", 1, "25.00%"], table.Sections[0].Footer);
        Assert.Equal(["შემთხვევების რაოდენობა:", 3, "75.00%"], table.Sections[1].Footer);
        Assert.Equal(["სულ:", 4, null], Assert.Single(table.FooterRows));
    }

    // Access's Percent format: two decimals, rounded half away from zero
    [Fact]
    public void TeacherMissAndSubstitutes_SharesHaveTwoDecimals()
    {
        // Arrange: 1 of 3 and 2 of 3
        List<PeriodLessonRow> lessons =
        [
            Lesson(1, At(1, 10), Cancelled), Lesson(2, At(2, 10), Cancelled, teacher: TeacherZ),
            Lesson(3, At(3, 10), Cancelled, teacher: TeacherZ)
        ];

        // Act
        ReportTable table = LessonCheckReports.TeacherMissAndSubstitutes(lessons);

        // Assert
        Assert.Equal(["33.33%", "66.67%"], table.Sections.Select(s => s.Footer![2]));
    }

    // two lessons at one time in one group code (another year's group): by the lesson
    [Fact]
    public void WrongStatusLessons_OneTimeAndGroupCode_ByLesson()
    {
        // Act
        ReportTable table = LessonCheckReports.WrongStatusLessons([
            Lesson(2, At(1, 10)), Lesson(1, At(1, 10), teacher: TeacherZ)
        ]);

        // Assert
        Assert.Equal(["01 10|G1|Zeta Zoe / T6", "01 10|G1|Alpha Ann / T5"], Rows(table).Select(Text));
    }

    // two errors of one lesson: by the log row
    [Fact]
    public void LessonsWithErrors_OneLesson_ByLogRow()
    {
        // Act
        ReportTable table = LessonCheckReports.LessonsWithErrors([
            new LessonErrorRow(2, "G1", At(1, 10), "error 14"), new LessonErrorRow(1, "G1", At(1, 10), "error 11")
        ]);

        // Assert
        Assert.Equal(["G1|01 10|error 11", "G1|01 10|error 14"], Rows(table).Select(Text));
    }

    // one group: by the lesson time, then the lesson
    [Fact]
    public void WrongWeekDayChanges_OneGroup_ByTimeAndLesson()
    {
        // Act
        ReportTable table = LessonCheckReports.WrongWeekDayChanges([
            new TeoDatesLessonRow(3, "G1", TeacherA, At(1, 0), At(29, 15), 8f, At(9, 15)),
            new TeoDatesLessonRow(2, "G1", TeacherA, At(1, 0), At(29, 15), 8f, At(2, 15)),
            new TeoDatesLessonRow(1, "G1", TeacherZ, At(1, 0), At(29, 15), 8f, At(2, 15))
        ]);

        // Assert
        Assert.Equal(["Zeta Zoe / T6|02 15", "Alpha Ann / T5|02 15", "Alpha Ann / T5|09 15"],
            Rows(table).Select(r => Text([r[1], r[5]])));
    }

    // one teacher's two cases at one time: by the lesson
    [Fact]
    public void TeacherMissAndSubstitutes_OneTime_ByLesson()
    {
        // Act
        ReportTable table = LessonCheckReports.TeacherMissAndSubstitutes([
            Lesson(2, At(1, 10), substitute: TeacherZ), Lesson(1, At(1, 10), Cancelled)
        ]);

        // Assert
        Assert.Equal(["01 10||status2", "01 10|Zeta Zoe / T6|status1"], Assert.Single(table.Sections).Rows.Select(Text));
    }

    // no cases: no sections, the total is 0
    [Fact]
    public void TeacherMissAndSubstitutes_NoCases()
    {
        // Act
        ReportTable table = LessonCheckReports.TeacherMissAndSubstitutes([Lesson(1, At(1, 10))]);

        // Assert
        Assert.Empty(table.Sections);
        Assert.Equal(["სულ:", 0, null], Assert.Single(table.FooterRows));
    }
}
