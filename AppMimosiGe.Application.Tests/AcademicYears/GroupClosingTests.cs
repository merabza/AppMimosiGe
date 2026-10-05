using System;
using AppMimosiGe.Application.AcademicYears.Models;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.AcademicYears;

public sealed class GroupClosingTests
{
    private static readonly DateTime CloseDate = Date(9, 1, 2027);

    private static DateTime Date(int month, int day, int year, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static Lesson Lesson(int id, DateTime lessonDt, int statusId, params LessonByStudent[] students)
    {
        var lesson = new Lesson { Id = id, LessonDt = lessonDt, LessonStatusId = statusId };
        foreach (LessonByStudent student in students)
        {
            lesson.LessonsByStudents.Add(student);
        }

        return lesson;
    }

    private static LessonByStudent Student(int id, int studentContractId = 10, int? gbsId = 1, float hours = 1.5f) =>
        new() { Id = id, StudentContractId = studentContractId, GroupByStudentId = gbsId, HoursCount = hours };

    //a group with an open teacher row, an open and an ended student row, a schedule row ending on the close date
    private static Group Group()
    {
        var group = new Group { GrpId = 42, GroupCode = "E1", DirtyLessons = false };
        group.GroupsByTeachers.Add(new GroupByTeacher { Id = 1, StartDate = Date(9, 1, 2026) });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 1, StudentContractId = 10, FourWeekFee = 48m, FourWeekHours = 8f, StartDate = Date(9, 1, 2026)
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 2,
            StudentContractId = 11,
            FourWeekFee = 60m,
            FourWeekHours = 6f,
            StartDate = Date(9, 1, 2026),
            EndDate = Date(5, 1, 2027)
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 1, StartDate = Date(9, 1, 2026), EndDate = CloseDate
        });
        //before the close date: not counted
        group.Lessons.Add(Lesson(300, Date(8, 31, 2027, 15), 1, Student(400)));
        //1.5 hours of 48 / 8 = -9 and 1 hour of 60 / 6 = -10 (the ended row still has its rate)
        group.Lessons.Add(Lesson(301, Date(9, 6, 2027, 15), 1, Student(401), Student(402, 11, 2, 1f)));
        //a cancelled lesson is no charge
        group.Lessons.Add(Lesson(302, Date(9, 13, 2027, 15), 2, Student(403)));
        //a student without a row or with the row of another contract is no charge
        group.Lessons.Add(Lesson(303, Date(9, 20, 2027, 15), 1, Student(404, gbsId: null), Student(405, 11, 1)));
        //exactly at the close date: counted (2 hours of 48 / 8 = -12)
        group.Lessons.Add(Lesson(304, CloseDate, 1, Student(406, hours: 2f)));
        return group;
    }

    [Fact]
    public void Apply_VoidsTheGroupAndMakesItDirty()
    {
        Group group = Group();
        group.VoidDate = Date(12, 1, 2027);

        GroupClosing closing = GroupClosing.Apply(group, CloseDate);

        Assert.Equal((CloseDate, true), (group.VoidDate, group.DirtyLessons));
        Assert.Equal(Date(12, 1, 2027), closing.PreviousVoidDate);
    }

    [Fact]
    public void Apply_CountsTheRowsThatAreOpenOnTheCloseDate()
    {
        GroupClosing closing = GroupClosing.Apply(Group(), CloseDate);

        Assert.Equal((1, 1, 1),
            (closing.OpenTeacherRowsCount, closing.OpenStudentRowsCount, closing.OpenScheduleRowsCount));
    }

    [Fact]
    public void Apply_ChargesEveryLessonFromTheCloseDateWithTheBalanceFormula()
    {
        GroupClosing closing = GroupClosing.Apply(Group(), CloseDate);

        Assert.Equal([301, 303, 304], closing.LessonCharges.Keys);
        Assert.Equal(-19m, closing.LessonCharges[301]);
        Assert.Equal(0m, closing.LessonCharges[303]);
        Assert.Equal(-12m, closing.LessonCharges[304]);
    }

    [Fact]
    public void DeletedLessonsCharges_SumsOnlyTheDeletedLessons()
    {
        GroupClosing closing = GroupClosing.Apply(Group(), CloseDate);

        Assert.Equal(-31m, closing.DeletedLessonsCharges([301, 303, 304, 999]));
        Assert.Equal(-19m, closing.DeletedLessonsCharges([301]));
        Assert.Equal(0m, closing.DeletedLessonsCharges([]));
    }

    // the sum is rounded like the balances (4 decimals): 1 hour of 50 / 3 is -16.66666…
    [Fact]
    public void DeletedLessonsCharges_IsRoundedToFourDecimals()
    {
        var group = new Group { GrpId = 43, GroupCode = "E2" };
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 1, StudentContractId = 10, FourWeekFee = 50m, FourWeekHours = 3f, StartDate = Date(9, 1, 2026)
        });
        group.Lessons.Add(Lesson(400, Date(9, 6, 2027, 15), 1, Student(500, hours: 1f)));

        Assert.Equal(-16.6667m, GroupClosing.Apply(group, CloseDate).DeletedLessonsCharges([400]));
    }
}
