using System;
using System.Collections.Generic;
using AppMimosiGe.Application.WorkHours.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.WorkHours;

public sealed class WorkHoursAutoGeneratorTests
{
    private static readonly DateTime Today = Day(10, 2);

    //the contract times are stored on Access's zero day
    private static readonly DateTime ZeroDay = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly DateTime ContractDate = new(2020, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static DateTime At(int month, int day, int hour, int minute = 0) =>
        Day(month, day).AddHours(hour).AddMinutes(minute);

    //contract 1 works 12:00-18:00 and 5 works 18:00-20:00 (fixed), 15 follows the lessons (no fixed hours)
    private static WorkHourEmployee Employee(int id, int? startHour = null, int? endHour = null,
        DateTime? contractDate = null, DateTime? contractEndDate = null)
    {
        return new WorkHourEmployee(id, $"Employee {id}", contractDate ?? ContractDate, contractEndDate,
            startHour is null ? null : ZeroDay.AddHours(startHour.Value),
            endHour is null ? null : ZeroDay.AddHours(endHour.Value));
    }

    private static LessonTimeData Lesson(DateTime lessonDt, float hoursCount = 1f) => new(lessonDt, hoursCount);

    private static List<GeneratedWorkHour> Plan(IReadOnlyCollection<WorkHourEmployee> employees,
        IEnumerable<LessonTimeData> lessons, IEnumerable<WorkHourDay>? existing = null, DateTime? dateFrom = null,
        DateTime? dateTo = null, DateTime? today = null)
    {
        return WorkHoursAutoGenerator.Plan(employees, lessons, existing ?? [], dateFrom ?? Day(9, 1),
            dateTo ?? Day(9, 30), today ?? Today);
    }

    // a contract with fixed hours gets them on every day the centre had a lesson, whatever the lessons' times
    [Fact]
    public void Plan_FixedHours_AreTheContractTimesOnTheLessonDay()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 12, 18)], [Lesson(At(9, 15, 9)), Lesson(At(9, 15, 19))]);

        Assert.Equal([new GeneratedWorkHour(1, At(9, 15, 12), At(9, 15, 18))], plan);
    }

    // without fixed hours: the first lesson's start and the last lesson's end in the whole centre that day; Access
    // cut DateAdd("h", 1.5, …) to one hour, here a lesson of 1.5 hours ends 1:30 after its start
    [Fact]
    public void Plan_WithoutFixedHours_IsTheCentresFirstAndLastLesson()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(15)],
            [Lesson(At(9, 15, 13, 30), 2f), Lesson(At(9, 15, 9, 30), 1.5f), Lesson(At(9, 15, 19), 1.5f)]);

        Assert.Equal([new GeneratedWorkHour(15, At(9, 15, 9, 30), At(9, 15, 20, 30))], plan);
    }

    // the end is the latest end of any student row: rows of one lesson may have different hours
    [Fact]
    public void Plan_LastEnd_IsTheLongestStudentRow()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(15)],
            [Lesson(At(9, 15, 18), 1.5f), Lesson(At(9, 15, 18), 2f), Lesson(At(9, 15, 18))]);

        Assert.Equal([new GeneratedWorkHour(15, At(9, 15, 18), At(9, 15, 20))], plan);
    }

    // a fixed start with the lessons' end, and the other way round
    [Fact]
    public void Plan_OneFixedTime_TakesTheOtherFromTheLessons()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(2, 10), Employee(3, endHour: 21)],
            [Lesson(At(9, 15, 11)), Lesson(At(9, 15, 17), 1.5f)]);

        Assert.Equal([
            new GeneratedWorkHour(2, At(9, 15, 10), At(9, 15, 18, 30)),
            new GeneratedWorkHour(3, At(9, 15, 11), At(9, 15, 21))
        ], plan);
    }

    // only the time of the contract's fixed hours counts, not their (zero) day
    [Fact]
    public void Plan_FixedHours_UseOnlyTheTimeOfDay()
    {
        var employee = new WorkHourEmployee(1, "Employee 1", ContractDate, null,
            new DateTime(2000, 1, 1, 12, 15, 0, DateTimeKind.Unspecified), ZeroDay.AddHours(18));

        Assert.Equal([new GeneratedWorkHour(1, At(9, 15, 12, 15), At(9, 15, 18))],
            Plan([employee], [Lesson(At(9, 15, 9))]));
    }

    // days and employees in order, one record each
    [Fact]
    public void Plan_ListsTheDaysInOrderAndTheEmployeesByContract()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(5, 18, 20), Employee(1, 12, 18)],
            [Lesson(At(9, 16, 10)), Lesson(At(9, 15, 10)), Lesson(At(9, 16, 15))]);

        Assert.Equal([
            new GeneratedWorkHour(1, At(9, 15, 12), At(9, 15, 18)),
            new GeneratedWorkHour(5, At(9, 15, 18), At(9, 15, 20)),
            new GeneratedWorkHour(1, At(9, 16, 12), At(9, 16, 18)),
            new GeneratedWorkHour(5, At(9, 16, 18), At(9, 16, 20))
        ], plan);
    }

    // a day without lessons is no working day, even for fixed hours
    [Fact]
    public void Plan_WithoutLessons_CreatesNothing()
    {
        Assert.Empty(Plan([Employee(1, 12, 18)], []));
    }

    // a day the employee already has a record on is skipped for that employee only (the start's day counts)
    [Fact]
    public void Plan_ExistingRecordDay_IsSkippedForItsEmployee()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 12, 18), Employee(5, 18, 20)],
            [Lesson(At(9, 15, 10)), Lesson(At(9, 16, 10))], [new WorkHourDay(1, At(9, 15, 23, 30))]);

        Assert.Equal([
            new GeneratedWorkHour(5, At(9, 15, 18), At(9, 15, 20)),
            new GeneratedWorkHour(1, At(9, 16, 12), At(9, 16, 18)),
            new GeneratedWorkHour(5, At(9, 16, 18), At(9, 16, 20))
        ], plan);
    }

    // only the days before today: today's and later lessons are no finished working days
    [Fact]
    public void Plan_TodayAndLater_AreSkipped()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 12, 18)],
            [Lesson(At(10, 1, 10)), Lesson(At(10, 2, 10)), Lesson(At(10, 3, 10))], dateFrom: Day(10, 1),
            dateTo: Day(10, 31), today: At(10, 2, 15));

        Assert.Equal([new GeneratedWorkHour(1, At(10, 1, 12), At(10, 1, 18))], plan);
    }

    // the start must be in the period, both days included
    [Fact]
    public void Plan_StartOutsideThePeriod_IsSkipped()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 12, 18)],
            [Lesson(At(9, 14, 10)), Lesson(At(9, 15, 10)), Lesson(At(9, 16, 10)), Lesson(At(9, 17, 10))],
            dateFrom: At(9, 15, 8), dateTo: At(9, 16, 8));

        Assert.Equal([
            new GeneratedWorkHour(1, At(9, 15, 12), At(9, 15, 18)),
            new GeneratedWorkHour(1, At(9, 16, 12), At(9, 16, 18))
        ], plan);
    }

    // the period's bounds are exact: a start at the first instant of its first day is in it, a start at the first
    // instant of the day after its last day is not
    [Fact]
    public void Plan_StartOnThePeriodBounds()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 0, 18)], [Lesson(At(9, 15, 10)), Lesson(At(9, 16, 10))],
            dateFrom: Day(9, 15), dateTo: Day(9, 15));

        Assert.Equal([new GeneratedWorkHour(1, Day(9, 15), At(9, 15, 18))], plan);
    }

    // the start must be before the end: a fixed start after the lessons' end, or equal to it, makes no record
    [Fact]
    public void Plan_StartNotBeforeEnd_IsSkipped()
    {
        List<GeneratedWorkHour> plan = Plan([Employee(1, 18), Employee(2, 17)],
            [Lesson(At(9, 15, 16)), Lesson(At(9, 16, 16), 1.5f)]);

        Assert.Equal([new GeneratedWorkHour(2, At(9, 16, 17), At(9, 16, 17, 30))], plan);
    }

    // unlike Access, only the days the contract is in force: from its date up to its end date, both included
    [Fact]
    public void Plan_DaysOutsideTheContract_AreSkipped()
    {
        List<GeneratedWorkHour> plan = Plan(
            [Employee(1, 12, 18, Day(9, 15), Day(9, 16)), Employee(2, 12, 18, contractEndDate: Day(9, 14))],
            [Lesson(At(9, 14, 10)), Lesson(At(9, 15, 10)), Lesson(At(9, 16, 10)), Lesson(At(9, 17, 10))]);

        Assert.Equal([
            new GeneratedWorkHour(2, At(9, 14, 12), At(9, 14, 18)),
            new GeneratedWorkHour(1, At(9, 15, 12), At(9, 15, 18)),
            new GeneratedWorkHour(1, At(9, 16, 12), At(9, 16, 18))
        ], plan);
    }
}
