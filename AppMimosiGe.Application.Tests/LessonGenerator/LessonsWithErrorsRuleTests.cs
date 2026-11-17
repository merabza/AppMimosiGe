using System;
using System.Collections.Generic;
using AppMimosiGe.Application.LessonGenerator.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class LessonsWithErrorsRuleTests
{
    private const int ExtraLesson = LessonGeneratorErrorCodes.ExtraLessonHasEnteredData;
    private const int ExtraStudent = LessonGeneratorErrorCodes.ExtraStudentHasEnteredData;

    //the default Monday group with the students of contracts 10 (row 1) and 11 (row 2) and their lessons of the
    //7th (100: rows 200 and 201) and the 14th (101: rows 210 and 211); enteredData: the rows with attendance or text
    private static GroupLessonsInput Group(int[] enteredData, DateTime? voidDate = null, DateTime? endDate10 = null,
        DateTime? endDate11 = null)
    {
        return Input(students: [Student(endDate: endDate10), Student(2, 11, endDate: endDate11)], voidDate: voidDate,
            lessons:
            [
                Lesson(100, SeptemberMonday(7), Row(200, 10, 1, enteredData), Row(201, 11, 2, enteredData)),
                Lesson(101, SeptemberMonday(14), Row(210, 10, 1, enteredData), Row(211, 11, 2, enteredData))
            ]);
    }

    private static ExistingLessonStudent Row(int id, int studentContractId, int groupByStudentId, int[] enteredData)
    {
        return LessonStudent(id, studentContractId, groupByStudentId, hasEnteredData: enteredData.Contains(id));
    }

    private static IReadOnlyList<ErroneousLessonStudent> NewErrors(GroupLessonsInput before, GroupLessonsInput after)
    {
        return LessonsWithErrorsRule.NewErrors(before, after, SeptemberEnd);
    }

    private static ErroneousLessonStudent OnThe14th(int errorCode, int rowId, int studentContractId)
    {
        return new ErroneousLessonStudent(errorCode, 101, Date(9, 14, 15), rowId, studentContractId);
    }

    [Fact]
    public void NewErrors_NothingChanges_FindsNothing()
    {
        GroupLessonsInput group = Group([200, 201, 210, 211]);

        Assert.Empty(NewErrors(group, group));
    }

    //the lesson of the 14th starts after the void date: the generator would keep it for its entered data (error 11)
    [Fact]
    public void NewErrors_VoidDateBeforeALessonWithEnteredData_ItsRowsWithDataAreNew()
    {
        Assert.Equal([OnThe14th(ExtraLesson, 210, 10)], NewErrors(Group([200, 210]), Group([200, 210], Date(9, 14))));
    }

    //the generator simply deletes an extra lesson without entered data
    [Fact]
    public void NewErrors_ExtraLessonWithoutEnteredData_IsNoError()
    {
        Assert.Empty(NewErrors(Group([200]), Group([200], Date(9, 14))));
    }

    //a schedule on another week day leaves both Monday lessons extra
    [Fact]
    public void NewErrors_ScheduleMovedToAnotherWeekDay_LessonsWithEnteredDataAreNew()
    {
        GroupLessonsInput before = Group([200]);

        Assert.Equal([new ErroneousLessonStudent(ExtraLesson, 100, Date(9, 7, 15), 200, 10)],
            NewErrors(before, before with { DayTimePlaces = [Schedule(Wednesday)] }));
    }

    //contract 11 leaves the group on the 14th: its row in that lesson stays for its entered data (error 14)
    [Fact]
    public void NewErrors_StudentLeavesBeforeHisRowWithEnteredData_TheRowIsNew()
    {
        Assert.Equal([OnThe14th(ExtraStudent, 211, 11)],
            NewErrors(Group([210, 211]), Group([210, 211], endDate11: Date(9, 14))));
    }

    [Fact]
    public void NewErrors_ErrorThatExistedBefore_IsNotNew()
    {
        Assert.Empty(NewErrors(Group([211], endDate11: Date(9, 14)),
            Group([211], endDate10: Date(9, 21), endDate11: Date(9, 14))));
    }

    //the rows of an extra lesson were already erroneous: the lesson is back, one of its students is still extra
    [Fact]
    public void NewErrors_ExtraLessonComesBackWithAnExtraStudent_IsNotNew()
    {
        Assert.Empty(NewErrors(Group([210, 211], Date(9, 14), endDate11: Date(9, 14)),
            Group([210, 211], endDate11: Date(9, 14))));
    }

    [Fact]
    public void NewErrors_LessonOfAnExtraStudentBecomesExtra_TheOtherRowsWithDataAreNew()
    {
        Assert.Equal([OnThe14th(ExtraLesson, 210, 10)],
            NewErrors(Group([210, 211], endDate11: Date(9, 14)),
                Group([210, 211], Date(9, 14), endDate11: Date(9, 14))));
    }

    //errors are compared by student row, not by lesson: another student of an erroneous lesson is a new error
    [Fact]
    public void NewErrors_AnotherStudentOfTheLessonBecomesExtra_IsNew()
    {
        Assert.Equal([OnThe14th(ExtraStudent, 210, 10)],
            NewErrors(Group([210, 211], endDate11: Date(9, 14)), Group([210, 211], endDate10: Date(9, 14))));
    }

    //the journal: data entered for a student the generator would delete from the lesson
    [Fact]
    public void NewErrors_EnteredDataAddedToAnExtraRow_IsNew()
    {
        Assert.Equal([OnThe14th(ExtraStudent, 211, 11)],
            NewErrors(Group([], endDate11: Date(9, 14)), Group([211], endDate11: Date(9, 14))));
    }

    [Fact]
    public void NewErrors_EnteredDataClearedFromAnErroneousRow_FindsNothing()
    {
        Assert.Empty(NewErrors(Group([211], endDate11: Date(9, 14)), Group([], endDate11: Date(9, 14))));
    }

    //the generator does not check a group without a schedule (error 3) and leaves its lessons as they are
    [Fact]
    public void NewErrors_GroupWithoutSchedule_FindsNothing()
    {
        GroupLessonsInput before = Group([210]);

        Assert.Empty(NewErrors(before, before with { DayTimePlaces = [] }));
    }
}
