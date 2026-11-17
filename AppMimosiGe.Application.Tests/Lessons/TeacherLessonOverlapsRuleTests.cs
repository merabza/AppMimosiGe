using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Lessons.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class TeacherLessonOverlapsRuleTests
{
    private const int Held = 1;
    private static readonly DateTime LessonDt = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Unspecified);

    private static DateTime At(int month, int day, int hour, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    // the substitute teaches the lesson when there is one, otherwise the lesson's teacher
    [Theory]
    [InlineData(null, 3)]
    [InlineData(5, 5)]
    public void TeacherTime_HeldLesson_IsItsTeacherStartAndHours(int? substituteTeacherContractId, int expected)
    {
        Assert.Equal(new LessonTeacherTime(expected, LessonDt, 1.5f),
            TeacherLessonOverlapsRule.TeacherTime(Held, 3, substituteTeacherContractId, LessonDt, null, 1.5f));
    }

    // a cancelled lesson (either kind) takes nobody's time
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void TeacherTime_CancelledLesson_IsNull(int lessonStatusId)
    {
        Assert.Null(TeacherLessonOverlapsRule.TeacherTime(lessonStatusId, 3, 5, LessonDt, null, 1.5f));
    }

    [Fact]
    public void TeacherTime_LessonWithoutHours_IsNull()
    {
        Assert.Null(TeacherLessonOverlapsRule.TeacherTime(Held, 3, null, LessonDt, null, 0f));
    }

    // a recovered lesson is held on the recover day at the lesson's time (r36)
    [Fact]
    public void TeacherTime_RecoveredLesson_StartsOnTheRecoverDay()
    {
        Assert.Equal(At(10, 5, 15),
            TeacherLessonOverlapsRule.TeacherTime(Held, 3, null, LessonDt, At(10, 5, 0), 1.5f)!.Start);
    }

    [Fact]
    public void ActualStart_IgnoresTheTimeOfTheRecoverDate()
    {
        Assert.Equal(At(10, 5, 15), TeacherLessonOverlapsRule.ActualStart(LessonDt, At(10, 5, 9, 30)));
        Assert.Equal(LessonDt, TeacherLessonOverlapsRule.ActualStart(LessonDt, null));
    }

    // a lesson of the day before can still run into this one; one that starts the next day only when this one passes
    // midnight
    [Fact]
    public void SearchDays_AreTheDayBeforeUntilTheDayAfterTheEnd()
    {
        Assert.Equal((At(9, 29, 0), At(10, 1, 0)),
            TeacherLessonOverlapsRule.SearchDays(new LessonTeacherTime(3, LessonDt, 1.5f)));
        Assert.Equal((At(9, 29, 0), At(10, 2, 0)),
            TeacherLessonOverlapsRule.SearchDays(new LessonTeacherTime(3, At(9, 30, 23), 2f)));
    }

    // [start, start + hours): a lesson that ends when the other starts is no overlap
    [Theory]
    [InlineData(13, 30, 1.5f, false)]
    [InlineData(13, 30, 2f, true)]
    [InlineData(15, 0, 0.5f, true)]
    [InlineData(16, 0, 1f, true)]
    [InlineData(16, 30, 1f, false)]
    public void Overlapping_OtherLessonOfTheSameDay(int hour, int minute, float hours, bool overlaps)
    {
        IReadOnlyList<TeacherLesson> result = TeacherLessonOverlapsRule.Overlapping(
            new LessonTeacherTime(3, LessonDt, 1.5f), [new TeacherLesson(77, "1002", At(9, 30, hour, minute), hours)]);

        Assert.Equal(overlaps, result.Count == 1);
    }

    [Fact]
    public void Overlapping_LessonOfTheDayBeforePastMidnight_Overlaps()
    {
        IReadOnlyList<TeacherLesson> result = TeacherLessonOverlapsRule.Overlapping(
            new LessonTeacherTime(3, At(9, 30, 0, 30), 1f), [new TeacherLesson(77, "1002", At(9, 29, 23), 2f)]);

        Assert.Single(result);
    }

    [Fact]
    public void Overlapping_AreInTheOrderOfStartThenId()
    {
        IReadOnlyList<TeacherLesson> result = TeacherLessonOverlapsRule.Overlapping(
            new LessonTeacherTime(3, LessonDt, 2f), [
                new TeacherLesson(9, "1003", At(9, 30, 16), 1f), new TeacherLesson(8, "1002", At(9, 30, 14), 2f),
                new TeacherLesson(7, "1004", At(9, 30, 16), 1f), new TeacherLesson(6, "1005", At(9, 30, 18), 1f)
            ]);

        Assert.Equal([8, 7, 9], result.Select(l => l.LessonId));
    }

    [Fact]
    public void Describe_ShowsTheGroupDayAndTimes()
    {
        Assert.Equal("ჯგუფი 1002, 30.09.2026 09:30–11:00",
            new TeacherLesson(77, "1002", At(9, 30, 9, 30), 1.5f).Describe());
    }
}
