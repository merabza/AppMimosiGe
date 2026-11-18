using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Requests;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupMapperTests
{
    private static readonly DateTime OtherDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);

    //the start times by id and the teacher contracts' default schemes the handler loads
    private static readonly Dictionary<int, TimeOnly> LessonStartTimes = new()
    {
        [17] = new TimeOnly(15, 0), [18] = new TimeOnly(17, 30)
    };

    private static readonly Dictionary<int, int> DefaultSalarySchemes = new() { [6] = DefaultSalarySchemeId };

    //the saved group: another void date and rows, one lesson
    private static GroupLessonsInput Current()
    {
        return new GroupLessonsInput(OtherDate, [new GeneratorTeacherRow(4, 3, OtherDate, null)],
            [new GeneratorStudentRow(1, 10, 1f, 8f, OtherDate, null)],
            [new GeneratorDayTimePlaceRow(2, new TimeOnly(9, 0), 1f, OtherDate, null)],
            [new ExistingLesson(100, new LessonValues(OtherDate, 4, 3, 8f, OtherDate, OtherDate), [])]);
    }

    //the request's void date, rows and dates as ApplyFields and ApplyRows save them (dates without time), with the
    //saved lessons
    [Fact]
    public void ToLessonsInput_TakesTheRequestsFieldsAndRowsAndKeepsTheLessons()
    {
        // Arrange
        GroupLessonsInput current = Current();
        GroupRequest request = ValidRequest(voidDate: new DateTime(2026, 12, 20, 13, 45, 0, DateTimeKind.Unspecified),
            teachers:
            [
                Teacher(100, endDate: OtherDate.AddHours(10)),
                Teacher(teacherContractId: 6, salarySchemaId: null, startDate: OtherDate)
            ], students:
            [
                Student(200, hoursCoefficient: 0.5f, endDate: OtherDate),
                Student(0, 21, 8, startDate: OtherDate.AddHours(9))
            ], dayTimePlaces: [DayTimePlace(300, 1, 17, 2f, endDate: OtherDate), DayTimePlace(0, 3, 18)]);

        // Act
        GroupLessonsInput input = GroupMapper.ToLessonsInput(request, current, DefaultSalarySchemes, LessonStartTimes);

        // Assert
        Assert.Equal(new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Unspecified), input.VoidDate);
        Assert.Equal([
            new GeneratorTeacherRow(5, 8, StartDate, OtherDate),
            new GeneratorTeacherRow(6, DefaultSalarySchemeId, OtherDate, null)
        ], input.Teachers);
        Assert.Equal([
            new GeneratorStudentRow(200, 20, 0.5f, 12f, StartDate, OtherDate),
            new GeneratorStudentRow(0, 21, 1f, 8f, OtherDate, null)
        ], input.Students);
        Assert.Equal([
            new GeneratorDayTimePlaceRow(1, new TimeOnly(15, 0), 2f, StartDate, OtherDate),
            new GeneratorDayTimePlaceRow(3, new TimeOnly(17, 30), 1.5f, StartDate, null)
        ], input.DayTimePlaces);
        Assert.Same(current.Lessons, input.Lessons);
    }

    [Fact]
    public void ToLessonsInput_RequestWithoutVoidDate_ClearsIt()
    {
        GroupLessonsInput input = GroupMapper.ToLessonsInput(ValidRequest(), Current(), DefaultSalarySchemes,
            LessonStartTimes);

        Assert.Null(input.VoidDate);
    }

    //D178: the group as ApplyFields and ApplyRows save it (code trimmed, dates without time), with the start times
    [Fact]
    public void ToTeacherSchedule_TakesTheRequestsVoidDateTeachersAndSchedule()
    {
        // Arrange
        GroupRequest request = ValidRequest(" 1011 ", new DateTime(2026, 12, 20, 13, 45, 0, DateTimeKind.Unspecified),
            [Teacher(100, endDate: OtherDate.AddHours(10)), Teacher(teacherContractId: 6, startDate: OtherDate)],
            dayTimePlaces: [DayTimePlace(300, 1, 17, 2f, endDate: OtherDate), DayTimePlace(0, 3, 18)]);

        // Act
        GroupSchedule schedule = GroupMapper.ToTeacherSchedule(42, request, LessonStartTimes);

        // Assert
        Assert.Equal(42, schedule.GrpId);
        Assert.Equal("1011", schedule.GroupCode);
        Assert.Equal(new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Unspecified), schedule.VoidDate);
        Assert.Equal([new GroupScheduleMember(5, StartDate, OtherDate), new GroupScheduleMember(6, OtherDate, null)],
            schedule.Members);
        Assert.Equal([
            new GroupScheduleDayTime(1, new TimeOnly(15, 0), 2f, StartDate, OtherDate),
            new GroupScheduleDayTime(3, new TimeOnly(17, 30), 1.5f, StartDate, null)
        ], schedule.DayTimes);
    }

    [Fact]
    public void ToTeacherSchedule_RequestWithoutVoidDate_HasNone()
    {
        Assert.Null(GroupMapper.ToTeacherSchedule(0, ValidRequest(), LessonStartTimes).VoidDate);
    }

    //D179: the same group with the students' rows instead of the teachers'
    [Fact]
    public void ToStudentSchedule_TakesTheRequestsVoidDateStudentsAndSchedule()
    {
        // Arrange
        GroupRequest request = ValidRequest(" 1011 ", new DateTime(2026, 12, 20, 13, 45, 0, DateTimeKind.Unspecified),
            [Teacher(100)],
            [Student(200, endDate: OtherDate.AddHours(10)), Student(studentContractId: 21, startDate: OtherDate)],
            [DayTimePlace(300, 1, 17, 2f, endDate: OtherDate), DayTimePlace(0, 3, 18)]);

        // Act
        GroupSchedule schedule = GroupMapper.ToStudentSchedule(42, request, LessonStartTimes);

        // Assert
        Assert.Equal(42, schedule.GrpId);
        Assert.Equal("1011", schedule.GroupCode);
        Assert.Equal(new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Unspecified), schedule.VoidDate);
        Assert.Equal([new GroupScheduleMember(20, StartDate, OtherDate), new GroupScheduleMember(21, OtherDate, null)],
            schedule.Members);
        Assert.Equal([
            new GroupScheduleDayTime(1, new TimeOnly(15, 0), 2f, StartDate, OtherDate),
            new GroupScheduleDayTime(3, new TimeOnly(17, 30), 1.5f, StartDate, null)
        ], schedule.DayTimes);
    }

    [Fact]
    public void ToStudentSchedule_RequestWithoutVoidDate_HasNone()
    {
        Assert.Null(GroupMapper.ToStudentSchedule(0, ValidRequest(), LessonStartTimes).VoidDate);
    }
}
