using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Lessons.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class StudentLessonOverlapsRuleTests
{
    private static readonly DateTime LessonDt = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Unspecified);

    private static DateTime At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    //the lesson 15:00–16:30 of 30.09
    private static IReadOnlyList<StudentLesson> Overlapping(params StudentLesson[] lessons) =>
        StudentLessonOverlapsRule.Overlapping(LessonDt, LessonDt.AddHours(1.5), lessons);

    // [start, start + hours): a lesson that ends when the other starts is no overlap
    [Theory]
    [InlineData(13, 30, 1.5f, false)]
    [InlineData(13, 30, 2f, true)]
    [InlineData(15, 0, 0.5f, true)]
    [InlineData(16, 0, 1f, true)]
    [InlineData(16, 30, 1f, false)]
    public void Overlapping_OtherLessonOfTheSameDay(int hour, int minute, float hours, bool overlaps)
    {
        IReadOnlyList<StudentLesson> result =
            Overlapping(new StudentLesson(10, 77, "1002", At(9, 30, hour, minute), hours));

        Assert.Equal(overlaps, result.Count == 1);
    }

    [Fact]
    public void Overlapping_LessonOfTheDayBeforePastMidnight_Overlaps()
    {
        IReadOnlyList<StudentLesson> result = StudentLessonOverlapsRule.Overlapping(At(9, 30, 0, 30), At(9, 30, 1, 30),
            [new StudentLesson(10, 77, "1002", At(9, 29, 23), 2f)]);

        Assert.Single(result);
    }

    [Fact]
    public void Overlapping_AreInTheOrderOfStartStudentAndLesson()
    {
        IReadOnlyList<StudentLesson> result = Overlapping(new StudentLesson(11, 9, "1003", At(9, 30, 16), 1f),
            new StudentLesson(10, 8, "1002", At(9, 30, 14), 2f), new StudentLesson(10, 9, "1003", At(9, 30, 16), 1f),
            new StudentLesson(10, 7, "1004", At(9, 30, 16), 1f), new StudentLesson(12, 6, "1005", At(9, 30, 18), 1f));

        Assert.Equal([(10, 8), (10, 7), (10, 9), (11, 9)], result.Select(l => (l.StudentContractId, l.LessonId)));
    }

    [Fact]
    public void Describe_ShowsTheStudentGroupDayAndTimes()
    {
        Assert.Equal("Gamma Gia — ჯგუფი 1002, 30.09.2026 09:30–11:00",
            new StudentLesson(10, 77, "1002", At(9, 30, 9, 30), 1.5f).Describe("Gamma Gia"));
    }
}
