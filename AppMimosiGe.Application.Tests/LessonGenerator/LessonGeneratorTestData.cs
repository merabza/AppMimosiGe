using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

//September 2026 starts on a Tuesday: Mondays are 7, 14, 21, 28, Wednesdays 2, 9, 16, 23, 30.
//October 2026: Mondays 5, 12, 19, 26, Wednesdays 7, 14, 21, 28
internal static class LessonGeneratorTestData
{
    public const int Monday = 1;
    public const int Wednesday = 3;
    public const int Sunday = 7;

    public static readonly DateTime SeptemberEnd = Date(9, 30);

    public static DateTime Date(int month, int day, int hour = 0, int minute = 0)
    {
        return new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    public static GeneratorTeacherRow Teacher(int teacherContractId = 5, int salarySchemaId = 8,
        DateTime? startDate = null, DateTime? endDate = null)
    {
        return new GeneratorTeacherRow(teacherContractId, salarySchemaId, startDate ?? Date(9, 1), endDate);
    }

    public static GeneratorStudentRow Student(int gbsId = 1, int studentContractId = 10, DateTime? startDate = null,
        DateTime? endDate = null, float fourWeekHours = 8f, float hoursCoefficient = 1f)
    {
        return new GeneratorStudentRow(gbsId, studentContractId, hoursCoefficient, fourWeekHours,
            startDate ?? Date(9, 1), endDate);
    }

    public static GeneratorDayTimePlaceRow Schedule(int weekDayId = Monday, int hour = 15, int minute = 0,
        float hoursCount = 1.5f, DateTime? startDate = null, DateTime? endDate = null)
    {
        return new GeneratorDayTimePlaceRow(weekDayId, new TimeOnly(hour, minute), hoursCount, startDate ?? Date(9, 1),
            endDate);
    }

    //a group with one teacher, one student and the Monday 15:00 lesson unless told otherwise
    public static GroupLessonsInput Input(IReadOnlyList<GeneratorTeacherRow>? teachers = null,
        IReadOnlyList<GeneratorStudentRow>? students = null, IReadOnlyList<GeneratorDayTimePlaceRow>? schedule = null,
        IReadOnlyList<ExistingLesson>? lessons = null, DateTime? voidDate = null)
    {
        return new GroupLessonsInput(voidDate, teachers ?? [Teacher()], students ?? [Student()],
            schedule ?? [Schedule()], lessons ?? []);
    }

    public static ExistingLessonStudent LessonStudent(int id, int studentContractId = 10, int? groupByStudentId = 1,
        float hoursCount = 1.5f, bool hasEnteredData = false)
    {
        return new ExistingLessonStudent(id, studentContractId, groupByStudentId, hoursCount, hasEnteredData);
    }

    public static ExistingLesson Lesson(int id, LessonValues values, params ExistingLessonStudent[] students)
    {
        return new ExistingLesson(id, values, students);
    }

    //the values the default group's September Monday lesson must have
    public static LessonValues SeptemberMonday(int day, int hour = 15, int minute = 0)
    {
        return new LessonValues(Date(9, day, hour, minute), 5, 8, 8f, Date(9, 7, hour, minute),
            Date(9, 28, hour, minute));
    }

    public static List<PlannedLessonChange> Created(GroupLessonsPlan plan)
    {
        return [.. plan.Changes.Where(c => c.Kind == ELessonChangeKind.Create)];
    }

    public static List<DateTime> CreatedLessonTimes(GroupLessonsPlan plan)
    {
        return [.. Created(plan).Select(c => c.Values.LessonDt)];
    }
}
