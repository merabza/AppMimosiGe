using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class CommentReportsTests
{
    private static readonly SchedulePerson Teacher = new("TLast", "TFirst", "T5");
    private static readonly SchedulePerson Substitute = new("ULast", "UFirst", "T6");

    private static DateTime At(int day, int hour, int minute = 0)
    {
        return new DateTime(2026, 9, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static CommentStudent Student(int contractId, string? teacherComment = null,
        string? studentComment = null)
    {
        return new CommentStudent(contractId, new SchedulePerson($"SLast{contractId}", "S", $"6.{contractId:000}"),
            teacherComment, studentComment);
    }

    private static CommentLesson Lesson(int id, string groupCode, DateTime lessonDt, params CommentStudent[] students)
    {
        return new CommentLesson(id, lessonDt, groupCode, "Math", Teacher, null, null, students);
    }

    private static string Text(List<object?> row)
    {
        return string.Join("|", row);
    }

    // a section per lesson by group code and time: group, course, teacher, time, the recovery date and the
    // substitute (blank to fill in by hand when there is none); the students numbered from 1 in each lesson, by name,
    // with their comments
    [Fact]
    public void DailyComments_ListsTheLessonsWithTheirStudents()
    {
        // Arrange
        CommentLesson[] lessons =
        [
            Lesson(3, "B1", At(1, 10), Student(1)),
            new(1, At(2, 15, 30), "A1", "Art", Teacher, Substitute, At(5, 0), [
                Student(2, "late", "ok"), Student(1, null, "first")
            ]),
            Lesson(2, "A1", At(1, 12), Student(3, "t"))
        ];

        // Act
        ReportTable table = CommentReports.DailyComments(lessons);

        // Assert
        Assert.Equal([
            ("number", "№", "wholeNumber"), ("student", "მოსწავლე", "text"),
            ("teacherComment", "მასწავლებლის კომენტარი", "text"), ("studentComment", "მოსწავლის კომენტარი", "text")
        ], table.Columns.Select(c => (c.Name, c.Caption, c.Type)));
        Assert.Equal([
            "ჯგუფი: A1 · საგანი: Math · მასწავლებელი: TLast TFirst / T5 · თარიღი: 01.09.2026 12:00 · " +
            "აღდგენის თარიღი:  · შემცვლელი მასწავლებელი: ",
            "ჯგუფი: A1 · საგანი: Art · მასწავლებელი: TLast TFirst / T5 · თარიღი: 02.09.2026 15:30 · " +
            "აღდგენის თარიღი: 05.09.2026 · შემცვლელი მასწავლებელი: ULast UFirst / T6",
            "ჯგუფი: B1 · საგანი: Math · მასწავლებელი: TLast TFirst / T5 · თარიღი: 01.09.2026 10:00 · " +
            "აღდგენის თარიღი:  · შემცვლელი მასწავლებელი: "
        ], table.Sections.Select(s => s.Header));
        Assert.Equal([
            ["1|SLast3 S / 6.003|t|"], ["1|SLast1 S / 6.001||first", "2|SLast2 S / 6.002|late|ok"],
            ["1|SLast1 S / 6.001||"]
        ], table.Sections.Select(s => s.Rows.Select(Text).ToArray()));
        Assert.All(table.Sections, s => Assert.Null(s.Footer));
        Assert.Empty(table.FooterRows);
    }

    // two lessons of a group at one time: by the lesson's id; one name twice: by the contract
    [Fact]
    public void DailyComments_Ties_ByLessonAndContract()
    {
        // Arrange
        var twin = new CommentStudent(7, new SchedulePerson("SLast1", "S", "6.001"), "seven", null);
        CommentLesson[] lessons =
        [
            Lesson(9, "A1", At(1, 10), Student(2)), Lesson(4, "A1", At(1, 10), twin, Student(1, "one"))
        ];

        // Act
        ReportTable table = CommentReports.DailyComments(lessons);

        // Assert
        Assert.Equal(["1|SLast1 S / 6.001|one|", "2|SLast1 S / 6.001|seven|"], table.Sections[0].Rows.Select(Text));
        Assert.Equal(["1|SLast2 S / 6.002||"], table.Sections[1].Rows.Select(Text));
    }
}
