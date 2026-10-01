using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class GroupLessonsPlannerTests
{
    private static GroupLessonsPlan Plan(GroupLessonsInput input, DateTime? horizonEnd = null)
    {
        return GroupLessonsPlanner.PlanGroup(input, horizonEnd ?? SeptemberEnd);
    }

    private static PlannedLessonChange CreatedOn(GroupLessonsPlan plan, DateTime day)
    {
        return Assert.Single(Created(plan), c => c.Values.LessonDt.Date == day);
    }

    private static List<(int StudentContractId, int? GroupByStudentId, float HoursCount)> Rows(
        PlannedLessonChange change)
    {
        return [.. change.Students.Select(s => (s.StudentContractId, s.GroupByStudentId, s.HoursCount))];
    }

    // --- an ordinary weekly schedule

    [Fact]
    public void PlanGroup_WeeklySchedule_CreatesALessonOnEveryScheduledDay()
    {
        // Arrange
        GroupLessonsInput input = Input(schedule:
        [
            Schedule(), Schedule(Wednesday, 17, hoursCount: 2f)
        ]);

        // Act
        GroupLessonsPlan plan = Plan(input);

        // Assert
        Assert.Equal([
            Date(9, 2, 17), Date(9, 7, 15), Date(9, 9, 17), Date(9, 14, 15), Date(9, 16, 17), Date(9, 21, 15),
            Date(9, 23, 17), Date(9, 28, 15), Date(9, 30, 17)
        ], CreatedLessonTimes(plan));
        //the first and the last lesson of the month over both week days
        Assert.All(Created(plan),
            c => Assert.Equal(new LessonValues(c.Values.LessonDt, 5, 8, 8f, Date(9, 2, 17), Date(9, 30, 17)),
                c.Values));
        Assert.Equal([(10, 1, 1.5f)], Rows(CreatedOn(plan, Date(9, 7))));
        Assert.Equal([(10, 1, 2f)], Rows(CreatedOn(plan, Date(9, 9))));
        Assert.All(Created(plan), c => Assert.Null(c.LessonId));
        Assert.Empty(plan.Logs);
        Assert.True(plan.ClearDirtyLessons);
        Assert.Null(plan.LastLesson);
    }

    [Fact]
    public void PlanGroup_ScheduleStartingInTheMiddleOfTheMonth_TeoDatesAreItsFirstAndLastLessonsInTheMonth()
    {
        GroupLessonsPlan plan = Plan(Input(schedule: [Schedule(startDate: Date(9, 15))]), Date(10, 31));

        Assert.Equal([
            Date(9, 21, 15), Date(9, 28, 15), Date(10, 5, 15), Date(10, 12, 15), Date(10, 19, 15),
            Date(10, 26, 15)
        ], CreatedLessonTimes(plan));
        PlannedLessonChange september = CreatedOn(plan, Date(9, 21));
        Assert.Equal(Date(9, 21, 15), september.Values.TeoMinDate);
        Assert.Equal(Date(9, 28, 15), september.Values.TeoMaxDate);
        PlannedLessonChange october = CreatedOn(plan, Date(10, 12));
        Assert.Equal(Date(10, 5, 15), october.Values.TeoMinDate);
        Assert.Equal(Date(10, 26, 15), october.Values.TeoMaxDate);
    }

    //a schedule active on the lesson day that has no day of its own in the month keeps the "inverted" defaults
    [Fact]
    public void PlanGroup_ActiveScheduleWithoutADayInTheMonth_DoesNotChangeTheTeoDates()
    {
        //Sunday from 28 September: no Sunday of its period is in September
        GroupLessonsPlan plan = Plan(Input(schedule:
        [
            Schedule(), Schedule(Sunday, 10, startDate: Date(9, 28), endDate: Date(10, 3))
        ]));

        PlannedLessonChange lesson = CreatedOn(plan, Date(9, 28));
        Assert.Equal(Date(9, 7, 15), lesson.Values.TeoMinDate);
        Assert.Equal(Date(9, 28, 15), lesson.Values.TeoMaxDate);
    }

    // --- teachers

    [Fact]
    public void PlanGroup_TeacherChangesInTheMiddleOfTheMonth_EachLessonGetsTheTeacherOfItsDay()
    {
        GroupLessonsPlan plan = Plan(Input([
            Teacher(5, 8, Date(9, 1), Date(9, 16)), Teacher(6, 9, Date(9, 16))
        ]));

        Assert.Equal([(5, 8), (5, 8), (6, 9), (6, 9)],
            Created(plan).Select(c => (c.Values.TeacherContractId, c.Values.SalarySchemaId)));
        Assert.Empty(plan.Logs);
    }

    [Fact]
    public void PlanGroup_TwoTeachersOnTheSameDay_LogsError5AndUsesTheFirstTeacher()
    {
        GroupLessonsPlan plan = Plan(Input([
            Teacher(5, 8, Date(9, 1)), Teacher(6, 9, Date(9, 10), Date(9, 20))
        ]));

        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.TwoTeachersOnDay, Date(9, 14), null)], plan.Logs);
        Assert.Equal(4, Created(plan).Count);
        Assert.All(Created(plan), c => Assert.Equal(5, c.Values.TeacherContractId));
    }

    [Fact]
    public void PlanGroup_DayWithoutTeacher_LogsError6AndCreatesNoLesson()
    {
        GroupLessonsPlan plan = Plan(Input([
            Teacher(5, 8, Date(9, 1), Date(9, 10)), Teacher(6, 9, Date(9, 20))
        ]));

        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.NoTeacherOnDay, Date(9, 14), null)], plan.Logs);
        Assert.Equal([Date(9, 7, 15), Date(9, 21, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
    }

    //teachers are checked only after the students: a day without both logs nothing
    [Fact]
    public void PlanGroup_DayWithoutTeacherAndWithoutStudents_LogsNothing()
    {
        GroupLessonsPlan plan = Plan(Input([Teacher(5, 8, Date(9, 1), Date(9, 10)), Teacher(6, 9, Date(9, 20))],
            [Student(startDate: Date(9, 1), endDate: Date(9, 10)), Student(2, 11, Date(9, 20))]));

        Assert.Empty(plan.Logs);
        Assert.Equal([Date(9, 7, 15), Date(9, 21, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
    }

    // --- students

    [Fact]
    public void PlanGroup_StudentsJoinAndLeave_EachLessonHasTheStudentsOfItsDay()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(2, 11, Date(9, 10)), Student(1, 10, Date(9, 1), Date(9, 15))
        ]));

        Assert.Equal([(10, 1, 1.5f)], Rows(CreatedOn(plan, Date(9, 7))));
        //ordered by the student contract
        Assert.Equal([(10, 1, 1.5f), (11, 2, 1.5f)], Rows(CreatedOn(plan, Date(9, 14))));
        Assert.Equal([(11, 2, 1.5f)], Rows(CreatedOn(plan, Date(9, 21))));
    }

    [Fact]
    public void PlanGroup_HoursCoefficient_MultipliesTheScheduleHours()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(hoursCoefficient: 0.5f), Student(2, 11, hoursCoefficient: 1.5f)
        ], schedule: [Schedule(hoursCount: 2f)]));

        Assert.Equal([(10, 1, 1f), (11, 2, 3f)], Rows(CreatedOn(plan, Date(9, 7))));
    }

    [Fact]
    public void PlanGroup_FourWeekHours_IsTheMaximumOfTheStudentsOfTheDay()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(), Student(2, 11, Date(9, 10), Date(9, 20), 12f)
        ]));

        Assert.Equal([8f, 12f, 8f, 8f], Created(plan).Select(c => c.Values.FourWeekHours));
    }

    [Fact]
    public void PlanGroup_DayWithoutStudents_CreatesNoLessonAndLogsNothing()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(1, 10, Date(9, 1), Date(9, 10)), Student(2, 11, Date(9, 20))
        ]));

        Assert.Equal([Date(9, 7, 15), Date(9, 21, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
        Assert.Empty(plan.Logs);
    }

    //D64: the first (lowest GbsId) of two active rows of one contract
    [Fact]
    public void PlanGroup_SameContractTwiceOnTheSameDay_UsesTheFirstRow()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(3, 10, Date(9, 1), fourWeekHours: 8f), Student(1, 10, Date(9, 10), fourWeekHours: 12f)
        ]));

        Assert.Equal([(10, 3, 1.5f)], Rows(CreatedOn(plan, Date(9, 7))));
        Assert.Equal([(10, 1, 1.5f)], Rows(CreatedOn(plan, Date(9, 14))));
        Assert.Equal(12f, CreatedOn(plan, Date(9, 14)).Values.FourWeekHours);
        Assert.Empty(plan.Logs);
    }

    // --- schedule

    [Fact]
    public void PlanGroup_ScheduleChangesWithAPeriod_LessonsAndTeoDatesFollowTheScheduleOfTheDay()
    {
        GroupLessonsPlan plan = Plan(Input(schedule:
        [
            Schedule(endDate: Date(9, 15)), Schedule(Monday, 16, 30, startDate: Date(9, 15))
        ]));

        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15), Date(9, 21, 16, 30), Date(9, 28, 16, 30)],
            CreatedLessonTimes(plan));
        PlannedLessonChange before = CreatedOn(plan, Date(9, 7));
        Assert.Equal((Date(9, 7, 15), Date(9, 14, 15)), (before.Values.TeoMinDate, before.Values.TeoMaxDate));
        PlannedLessonChange after = CreatedOn(plan, Date(9, 28));
        Assert.Equal((Date(9, 21, 16, 30), Date(9, 28, 16, 30)), (after.Values.TeoMinDate, after.Values.TeoMaxDate));
    }

    [Fact]
    public void PlanGroup_TwoSchedulesOnTheSameWeekDay_LogsError7AndUsesTheFirst()
    {
        GroupLessonsPlan plan = Plan(Input(schedule:
        [
            Schedule(), Schedule(Monday, 16, 30, startDate: Date(9, 10), endDate: Date(9, 20))
        ]));

        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.TwoDayTimePlacesOnDay, Date(9, 14), null)],
            plan.Logs);
        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15), Date(9, 21, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
        //both schedules are active on the 14th: the earliest and the latest of their lessons in September
        Assert.Equal((Date(9, 7, 15), Date(9, 28, 15)),
            (CreatedOn(plan, Date(9, 14)).Values.TeoMinDate, CreatedOn(plan, Date(9, 14)).Values.TeoMaxDate));
    }

    //the last day of the month is searched down to the 1st: a schedule whose only day in October is the 1st
    [Fact]
    public void PlanGroup_ScheduleWhoseOnlyDayInTheMonthIsTheFirst_TeoDatesAreThatLesson()
    {
        GroupLessonsPlan plan = Plan(Input(schedule: [Schedule(Thursday, endDate: Date(10, 2))]), Date(10, 31));

        PlannedLessonChange lesson = CreatedOn(plan, Date(10, 1));
        Assert.Equal((Date(10, 1, 15), Date(10, 1, 15)), (lesson.Values.TeoMinDate, lesson.Values.TeoMaxDate));
    }

    // --- periods

    //a period ends before its end day: a teacher ending on a lesson day no longer teaches that day
    [Fact]
    public void PlanGroup_RowEndingOnALessonDay_NoLongerCountsThatDay()
    {
        GroupLessonsPlan plan = Plan(Input([Teacher(5, 8, Date(9, 1), Date(9, 14)), Teacher(6, 9, Date(9, 14))]));

        Assert.Equal(6, CreatedOn(plan, Date(9, 14)).Values.TeacherContractId);
        Assert.Empty(plan.Logs);
    }

    // --- void date and the horizon

    [Fact]
    public void PlanGroup_VoidDate_NoLessonsFromThatDayOn()
    {
        GroupLessonsPlan plan = Plan(Input(voidDate: Date(9, 21)));

        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15)], CreatedLessonTimes(plan));
    }

    //Access compares the lesson time with the void date: a lesson at exactly that moment still takes place
    [Fact]
    public void PlanGroup_LessonExactlyAtTheVoidTime_StillTakesPlace()
    {
        GroupLessonsPlan plan = Plan(Input(voidDate: Date(9, 21, 15)));

        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15), Date(9, 21, 15)], CreatedLessonTimes(plan));
    }

    //the earliest of the latest ends stops the lessons before the teachers and students are checked: no error 6
    [Fact]
    public void PlanGroup_AllTeachersEnded_NoLessonsAndNoErrorsAfterTheirEnd()
    {
        GroupLessonsPlan plan = Plan(Input([Teacher(endDate: Date(9, 15))], [Student(endDate: Date(9, 22))],
            [Schedule(endDate: Date(9, 29))]));

        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15)], CreatedLessonTimes(plan));
        Assert.Empty(plan.Logs);
    }

    //D62: Access skipped the last day of the horizon when the group had a void date after the horizon
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlanGroup_LastDayOfTheHorizon_GetsItsLessonWithOrWithoutAVoidDate(bool withVoidDate)
    {
        GroupLessonsPlan plan = Plan(Input(voidDate: withVoidDate ? Date(10, 15) : null), Date(9, 28));

        Assert.Equal([Date(9, 7, 15), Date(9, 14, 15), Date(9, 21, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
    }

    //the check runs up to the latest existing lesson, not up to the earliest one
    [Fact]
    public void PlanGroup_ExistingLessonAfterTheHorizon_IsCheckedAndDeleted()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(99, SeptemberMonday(7), LessonStudent(199)),
            Lesson(100, new LessonValues(Date(10, 5, 15), 5, 8, 8f, Date(10, 5, 15), Date(10, 26, 15)),
                LessonStudent(200))
        ]));

        PlannedLessonChange deleted = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Delete);
        Assert.Equal(100, deleted.LessonId);
        Assert.Equal(3, Created(plan).Count);
    }

    // --- lessons that must not exist

    [Fact]
    public void PlanGroup_ExtraLessonWithoutEnteredData_IsDeletedWithItsStudents()
    {
        var tuesday = new LessonValues(Date(9, 8, 15), 5, 8, 8f, Date(9, 7, 15), Date(9, 28, 15));
        GroupLessonsPlan plan = Plan(Input(lessons:
            [Lesson(100, tuesday, LessonStudent(200), LessonStudent(201, 11))]));

        PlannedLessonChange deleted = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Delete);
        Assert.Equal(100, deleted.LessonId);
        Assert.Equal(tuesday, deleted.Values);
        Assert.Equal([(EStudentRowChangeKind.Delete, 200), (EStudentRowChangeKind.Delete, 201)],
            deleted.Students.Select(s => (s.Kind, s.RowId!.Value)));
        Assert.Empty(plan.Logs);
    }

    [Fact]
    public void PlanGroup_ExtraLessonWithEnteredData_StaysAndLogsError11()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, new LessonValues(Date(9, 8, 15), 5, 8, 8f, Date(9, 7, 15), Date(9, 28, 15)), LessonStudent(200),
                LessonStudent(201, 11, hasEnteredData: true))
        ]));

        Assert.DoesNotContain(plan.Changes, c => c.Kind == ELessonChangeKind.Delete);
        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.ExtraLessonHasEnteredData, Date(9, 8, 15), 100)],
            plan.Logs);
    }

    // --- existing lessons

    [Fact]
    public void PlanGroup_ExistingLessonsAsTheyMustBe_PlansNothingButClearsDirtyLessons()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7), LessonStudent(200)), Lesson(101, SeptemberMonday(14), LessonStudent(201)),
            Lesson(102, SeptemberMonday(21), LessonStudent(202)), Lesson(103, SeptemberMonday(28), LessonStudent(203))
        ]));

        Assert.Empty(plan.Changes);
        Assert.Empty(plan.Logs);
        Assert.Empty(plan.DirtyStudentContractIds);
        Assert.True(plan.ClearDirtyLessons);
    }

    [Fact]
    public void PlanGroup_ExistingLessonWithAnotherTime_IsUpdated()
    {
        var previous = new LessonValues(Date(9, 7, 14), 5, 8, 8f, Date(9, 7, 14), Date(9, 28, 14));
        GroupLessonsPlan plan = Plan(Input(lessons: [Lesson(100, previous, LessonStudent(200))]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Equal(100, updated.LessonId);
        Assert.Equal(SeptemberMonday(7), updated.Values);
        Assert.Equal(previous, updated.PreviousValues);
        Assert.Empty(updated.Students);
        Assert.Equal(3, Created(plan).Count);
    }

    //error 8 was not written by Access either: the lesson is corrected even with entered data
    [Fact]
    public void PlanGroup_ExistingLessonWithEnteredDataAndAnotherTeacher_IsStillUpdated()
    {
        LessonValues previous = SeptemberMonday(7) with { TeacherContractId = 99, SalarySchemaId = 98 };
        GroupLessonsPlan plan = Plan(Input(lessons: [Lesson(100, previous, LessonStudent(200, hasEnteredData: true))]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Equal(previous, updated.PreviousValues);
        Assert.Equal(SeptemberMonday(7), updated.Values);
        Assert.Empty(plan.Logs);
    }

    [Fact]
    public void PlanGroup_ExistingLessonWithAnotherFourWeekHoursOrTeoDates_IsUpdated()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7) with { FourWeekHours = 12f }, LessonStudent(200)),
            Lesson(101, SeptemberMonday(14) with { TeoMinDate = Date(9, 1) }, LessonStudent(201)),
            Lesson(102, SeptemberMonday(21) with { TeoMaxDate = Date(9, 30) }, LessonStudent(202)),
            Lesson(103, SeptemberMonday(28), LessonStudent(203))
        ]));

        Assert.Equal([100, 101, 102], plan.Changes.Select(c => c.LessonId!.Value));
        Assert.All(plan.Changes, c => Assert.Equal(ELessonChangeKind.Update, c.Kind));
    }

    [Fact]
    public void PlanGroup_MissingStudent_IsAddedToTheExistingLesson()
    {
        GroupLessonsPlan plan = Plan(Input(students: [Student(), Student(2, 11)],
            lessons: [Lesson(100, SeptemberMonday(7), LessonStudent(200))]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Null(updated.PreviousValues);
        PlannedStudentRow added = Assert.Single(updated.Students);
        Assert.Equal(new PlannedStudentRow(EStudentRowChangeKind.Add, null, 11, 2, 1.5f), added);
    }

    [Fact]
    public void PlanGroup_StudentRowWithOtherHoursOrGroupRow_IsUpdated()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7), LessonStudent(200, hoursCount: 1f)),
            Lesson(101, SeptemberMonday(14), LessonStudent(201, groupByStudentId: null)),
            //entered data does not stop the correction (error 13 was commented out in Access)
            Lesson(102, SeptemberMonday(21), LessonStudent(202, groupByStudentId: 7, hasEnteredData: true)),
            Lesson(103, SeptemberMonday(28), LessonStudent(203))
        ]));

        Assert.Equal([
            new PlannedStudentRow(EStudentRowChangeKind.Update, 200, 10, 1, 1.5f),
            new PlannedStudentRow(EStudentRowChangeKind.Update, 201, 10, 1, 1.5f),
            new PlannedStudentRow(EStudentRowChangeKind.Update, 202, 10, 1, 1.5f)
        ], plan.Changes.Select(c => Assert.Single(c.Students)));
        Assert.All(plan.Changes, c => Assert.Null(c.PreviousValues));
    }

    [Fact]
    public void PlanGroup_ExtraStudentWithoutEnteredData_IsDeleted()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
            [Lesson(100, SeptemberMonday(7), LessonStudent(200), LessonStudent(201, 99, null))]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Equal([new PlannedStudentRow(EStudentRowChangeKind.Delete, 201, 99, null, 1.5f)], updated.Students);
        Assert.Empty(plan.Logs);
    }

    [Fact]
    public void PlanGroup_ExtraStudentWithEnteredData_StaysAndLogsError14()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7), LessonStudent(199, 5, null, hasEnteredData: true), LessonStudent(200),
                LessonStudent(201, 99, null, hasEnteredData: true))
        ]));

        Assert.DoesNotContain(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        var log = new PlannedLogEntry(LessonGeneratorErrorCodes.ExtraStudentHasEnteredData, Date(9, 7, 15), 100);
        Assert.Equal([log, log], plan.Logs);
    }

    //the error 14 entry carries the corrected lesson time
    [Fact]
    public void PlanGroup_ExtraStudentOfAMovedLesson_LogsTheNewLessonTime()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7, 14), LessonStudent(200), LessonStudent(201, 99, null, hasEnteredData: true))
        ]));

        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.ExtraStudentHasEnteredData, Date(9, 7, 15), 100)],
            plan.Logs);
    }

    //an existing lesson can not have two rows of one contract unless entered by hand: the second one is extra
    [Fact]
    public void PlanGroup_TwoRowsOfOneContract_TheSecondIsExtra()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
            [Lesson(100, SeptemberMonday(7), LessonStudent(201), LessonStudent(200))]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Equal([new PlannedStudentRow(EStudentRowChangeKind.Delete, 201, 10, 1, 1.5f)], updated.Students);
    }

    //the other lesson is the earlier one: the lesson of the right time is found, not simply the first
    [Fact]
    public void PlanGroup_TwoLessonsOnOneDay_UsesTheOneWithTheRightTimeAndLeavesTheOther()
    {
        LessonValues other = SeptemberMonday(7, 12);
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, other, LessonStudent(200)), Lesson(101, SeptemberMonday(7), LessonStudent(201))
        ]));

        Assert.DoesNotContain(plan.Changes, c => c.LessonId is not null);
        Assert.Equal(3, Created(plan).Count);
    }

    [Fact]
    public void PlanGroup_TwoLessonsOnOneDayWithoutTheRightTime_UpdatesTheEarliest()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(101, SeptemberMonday(7, 19), LessonStudent(201)),
            Lesson(100, SeptemberMonday(7, 18), LessonStudent(200))
        ]));

        PlannedLessonChange updated = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Update);
        Assert.Equal(100, updated.LessonId);
        Assert.Equal(Date(9, 7, 15), updated.Values.LessonDt);
    }

    // --- the start date

    //D61: Access kept the lesson time in the start date and added it to every day of the loop. The check starts
    //at the earliest existing lesson, not at the latest one
    [Fact]
    public void PlanGroup_LessonBeforeTheGroupStart_IsCheckedAndTheLaterLessonTimesStayExact()
    {
        GroupLessonsPlan plan = Plan(Input(students: [Student(startDate: Date(9, 14))],
            lessons:
            [
                Lesson(100, SeptemberMonday(7), LessonStudent(200)),
                Lesson(101, SeptemberMonday(21), LessonStudent(201))
            ]));

        PlannedLessonChange deleted = Assert.Single(plan.Changes, c => c.Kind == ELessonChangeKind.Delete);
        Assert.Equal(100, deleted.LessonId);
        Assert.Equal([Date(9, 14, 15), Date(9, 28, 15)], CreatedLessonTimes(plan));
    }

    //the group starts on the 8th (its teacher): the 7th is not checked, so no error 6 is logged for it
    [Fact]
    public void PlanGroup_StartDate_IsTheLatestOfTheFirstStartsOfTeachersStudentsAndSchedule()
    {
        GroupLessonsPlan plan = Plan(Input([Teacher(startDate: Date(9, 8))], [Student(startDate: Date(9, 1))],
            [Schedule(startDate: Date(9, 1)), Schedule(Wednesday, 17, startDate: Date(9, 10))]));

        Assert.Equal(Date(9, 14, 15), CreatedLessonTimes(plan)[0]);
        Assert.Empty(plan.Logs);
    }

    // --- invalid groups

    //D63: Access wrote these errors twice
    [Theory]
    [InlineData(true, false, false, LessonGeneratorErrorCodes.NoTeachers)]
    [InlineData(false, true, false, LessonGeneratorErrorCodes.NoStudents)]
    [InlineData(false, false, true, LessonGeneratorErrorCodes.NoDayTimePlaces)]
    [InlineData(true, true, true, LessonGeneratorErrorCodes.NoTeachers)]
    public void PlanGroup_GroupWithoutTeachersStudentsOrSchedule_LogsOneErrorAndChangesNothing(bool noTeachers,
        bool noStudents, bool noSchedule, int expectedError)
    {
        GroupLessonsInput input = Input(noTeachers ? [] : null, noStudents ? [] : null, noSchedule ? [] : null,
            [Lesson(100, SeptemberMonday(7, 14), LessonStudent(200))]);

        GroupLessonsPlan plan = Plan(input);

        Assert.Equal([new PlannedLogEntry(expectedError, null, null)], plan.Logs);
        Assert.Empty(plan.Changes);
        Assert.Empty(plan.DirtyStudentContractIds);
        Assert.False(plan.ClearDirtyLessons);
    }

    // --- dirty flags

    [Fact]
    public void PlanGroup_LessonCreated_MarksEveryContractOfTheGroup()
    {
        GroupLessonsPlan plan = Plan(Input(students:
        [
            Student(1, 12), Student(2, endDate: Date(9, 2)), Student(3, 11, Date(10, 5))
        ]));

        Assert.Equal([10, 11, 12], plan.DirtyStudentContractIds);
    }

    [Fact]
    public void PlanGroup_OnlyAStudentRowChanged_MarksOnlyItsContract()
    {
        GroupLessonsPlan plan = Plan(Input(students: [Student(), Student(2, 11, endDate: Date(9, 2))], lessons:
        [
            Lesson(100, SeptemberMonday(7), LessonStudent(200, hoursCount: 1f)),
            Lesson(101, SeptemberMonday(14), LessonStudent(201)), Lesson(102, SeptemberMonday(21), LessonStudent(202)),
            Lesson(103, SeptemberMonday(28), LessonStudent(203))
        ]));

        Assert.Equal([10], plan.DirtyStudentContractIds);
    }

    //a deleted lesson row can belong to a contract that is not in the group any more
    [Fact]
    public void PlanGroup_LessonDeleted_MarksTheGroupContractsAndTheContractsOfItsRows()
    {
        GroupLessonsPlan plan = Plan(Input(lessons:
        [
            Lesson(100, SeptemberMonday(7), LessonStudent(200)), Lesson(101, SeptemberMonday(14), LessonStudent(201)),
            Lesson(102, SeptemberMonday(21), LessonStudent(202)), Lesson(103, SeptemberMonday(28), LessonStudent(203)),
            Lesson(104, new LessonValues(Date(9, 8, 15), 5, 8, 8f, Date(9, 7, 15), Date(9, 28, 15)),
                LessonStudent(204, 99, null))
        ]));

        Assert.Equal([10, 99], plan.DirtyStudentContractIds);
    }

    [Fact]
    public void PlanGroup_LessonFieldsUpdated_MarksEveryContractOfTheGroup()
    {
        GroupLessonsPlan plan = Plan(Input(students: [Student(), Student(2, 11, Date(10, 5))], lessons:
        [
            Lesson(100, SeptemberMonday(7) with { SalarySchemaId = 9 }, LessonStudent(200)),
            Lesson(101, SeptemberMonday(14), LessonStudent(201)), Lesson(102, SeptemberMonday(21), LessonStudent(202)),
            Lesson(103, SeptemberMonday(28), LessonStudent(203))
        ]));

        Assert.Equal([10, 11], plan.DirtyStudentContractIds);
    }

    // --- the last lesson

    [Fact]
    public void PlanLastLesson_NoLessonYet_PlansTheLastLessonDayUpToToday()
    {
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(Input(), SeptemberEnd, Date(9, 17, 10));

        Assert.Equal(new PlannedLastLesson(null, Date(9, 14, 15)), plan.LastLesson);
        Assert.Equal([Date(9, 14, 15)], CreatedLessonTimes(plan));
        Assert.False(plan.ClearDirtyLessons);
    }

    [Fact]
    public void PlanLastLesson_TodayIsALessonDay_ItIsTheLastLesson()
    {
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(Input(), SeptemberEnd, Date(9, 14));

        Assert.Equal(new PlannedLastLesson(null, Date(9, 14, 15)), plan.LastLesson);
    }

    [Fact]
    public void PlanLastLesson_ExistingLesson_ReturnsItAndFixesTheDaysOnTheWay()
    {
        var extra = new LessonValues(Date(9, 16, 15), 5, 8, 8f, Date(9, 7, 15), Date(9, 28, 15));
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(Input(lessons:
        [
            Lesson(100, SeptemberMonday(14, 14), LessonStudent(200)), Lesson(101, extra, LessonStudent(201)),
            Lesson(102, SeptemberMonday(21, 14), LessonStudent(202))
        ]), SeptemberEnd, Date(9, 17));

        Assert.Equal(new PlannedLastLesson(100, Date(9, 14, 15)), plan.LastLesson);
        //the extra lesson after it is deleted, the lesson itself corrected, the later lesson left as it is
        Assert.Equal([(ELessonChangeKind.Delete, 101), (ELessonChangeKind.Update, 100)],
            plan.Changes.Select(c => (c.Kind, c.LessonId!.Value)));
    }

    //the search goes back to the start day itself
    [Fact]
    public void PlanLastLesson_OnlyLessonDayIsTheStartDay_FindsIt()
    {
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(
            Input([Teacher(startDate: Date(9, 7))], [Student(startDate: Date(9, 7))],
                [Schedule(startDate: Date(9, 7))]), SeptemberEnd, Date(9, 10));

        Assert.Equal(new PlannedLastLesson(null, Date(9, 7, 15)), plan.LastLesson);
    }

    [Fact]
    public void PlanLastLesson_TodayBeforeTheGroupStart_ReturnsNothing()
    {
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(Input(), SeptemberEnd, Date(8, 31));

        Assert.Null(plan.LastLesson);
        Assert.Empty(plan.Changes);
        Assert.False(plan.ClearDirtyLessons);
    }

    [Fact]
    public void PlanLastLesson_InvalidGroup_ReturnsNothingAndLogsTheError()
    {
        GroupLessonsPlan plan = GroupLessonsPlanner.PlanLastLesson(Input([]), SeptemberEnd, Date(9, 17));

        Assert.Null(plan.LastLesson);
        Assert.Equal([new PlannedLogEntry(LessonGeneratorErrorCodes.NoTeachers, null, null)], plan.Logs);
        Assert.False(plan.ClearDirtyLessons);
    }
}
