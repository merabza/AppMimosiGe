using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.Lessons.UpdateLesson;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonMapperTests
{
    private static readonly DateTime LessonDt = new(2026, 9, 30, 15, 0, 0, DateTimeKind.Unspecified);

    private static Lesson GeneratedLesson()
    {
        return new Lesson
        {
            Id = 9,
            GroupId = 7,
            TeacherContractId = 3,
            LessonDt = LessonDt,
            SalarySchemaId = 4,
            FourWeekHours = 8f,
            TeoMinDate = LessonDt.AddDays(-2),
            TeoMaxDate = LessonDt.AddDays(2),
            LessonStatusId = 1,
            SubstituteTeacherContractId = 5,
            TeacherLateMinutes = 3,
            RecoverDate = LessonDt,
            Note = "old"
        };
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("\t\r\n", null)]
    [InlineData("theme", "theme")]
    [InlineData("  theme  ", "theme")]
    [InlineData(" two words ", "two words")]
    public void NormalizeText_EmptyIsNullAndTextIsTrimmed(string? value, string? expected)
    {
        Assert.Equal(expected, LessonMapper.NormalizeText(value));
    }

    [Fact]
    public void ApplyFields_CopiesTheEditableFieldsOnly()
    {
        // Arrange
        Lesson lesson = GeneratedLesson();
        var request = new LessonRequest
        {
            LessonStatusId = 2,
            SubstituteTeacherContractId = 6,
            TeacherLateMinutes = 12,
            RecoverDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified),
            Note = " new note "
        };

        // Act
        LessonMapper.ApplyFields(lesson, request);

        // Assert
        Assert.Equal(2, lesson.LessonStatusId);
        Assert.Equal(6, lesson.SubstituteTeacherContractId);
        Assert.Equal(12, lesson.TeacherLateMinutes);
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified), lesson.RecoverDate);
        Assert.Equal("new note", lesson.Note);
        Assert.Equal(9, lesson.Id);
        Assert.Equal(7, lesson.GroupId);
        Assert.Equal(3, lesson.TeacherContractId);
        Assert.Equal(LessonDt, lesson.LessonDt);
        Assert.Equal(4, lesson.SalarySchemaId);
        Assert.Equal(8f, lesson.FourWeekHours);
    }

    // the recovery is a day: a time sent by a client is dropped
    [Fact]
    public void ApplyFields_RecoverDateKeepsTheDayOnly()
    {
        // Arrange
        Lesson lesson = GeneratedLesson();

        var request = new LessonRequest
        {
            LessonStatusId = 1, RecoverDate = new DateTime(2026, 10, 5, 23, 59, 0, DateTimeKind.Unspecified)
        };

        // Act
        LessonMapper.ApplyFields(lesson, request);

        // Assert
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified), lesson.RecoverDate);
    }

    [Fact]
    public void ApplyFields_EmptyRequestClearsTheOptionalFields()
    {
        // Arrange
        Lesson lesson = GeneratedLesson();

        // Act
        LessonMapper.ApplyFields(lesson, new LessonRequest { LessonStatusId = 1, Note = "" });

        // Assert
        Assert.Null(lesson.SubstituteTeacherContractId);
        Assert.Equal(0, lesson.TeacherLateMinutes);
        Assert.Null(lesson.RecoverDate);
        Assert.Null(lesson.Note);
    }

    [Fact]
    public void ApplyStudentFields_CopiesTheJournalFieldsAndKeepsTheGeneratorFields()
    {
        // Arrange
        var row = new LessonByStudent
        {
            Id = 21,
            LessonId = 9,
            StudentContractId = 10,
            GroupByStudentId = 31,
            HoursCount = 1.5f
        };

        // Act
        LessonMapper.ApplyStudentFields(row,
            new LessonStudentRequest
            {
                Id = 99,
                Present = true,
                Theme = " Fractions ",
                Rate = 7.5f,
                TeacherComment = " good ",
                StudentComment = " ok ",
                StudentLateMinutes = 4
            });

        // Assert
        Assert.True(row.Present);
        Assert.Equal("Fractions", row.Theme);
        Assert.Equal(7.5f, row.Rate);
        Assert.Equal("good", row.TeacherComment);
        Assert.Equal("ok", row.StudentComment);
        Assert.Equal(4, row.StudentLateMinutes);
        Assert.Equal(21, row.Id);
        Assert.Equal(9, row.LessonId);
        Assert.Equal(10, row.StudentContractId);
        Assert.Equal(31, row.GroupByStudentId);
        Assert.Equal(1.5f, row.HoursCount);
    }

    // a cleared row: no entered data is left for the generator (D65, Q17)
    [Fact]
    public void ApplyStudentFields_ClearedRowHasNoEnteredData()
    {
        // Arrange
        var row = new LessonByStudent
        {
            Id = 21,
            Present = true,
            Theme = "theme",
            Rate = 9,
            TeacherComment = "tc",
            StudentComment = "sc",
            StudentLateMinutes = 5
        };

        // Act
        LessonMapper.ApplyStudentFields(row,
            new LessonStudentRequest { Id = 21, Theme = "", TeacherComment = " ", StudentComment = null });

        // Assert
        Assert.False(row.Present);
        Assert.Null(row.Theme);
        Assert.Null(row.Rate);
        Assert.Null(row.TeacherComment);
        Assert.Null(row.StudentComment);
        Assert.Equal(0, row.StudentLateMinutes);
    }

    //the saved group: lesson 9 with rows 21 (entered data) and 22 (none), and lesson 10 with row 31 (none)
    private static GroupLessonsInput SavedLessons()
    {
        var values = new LessonValues(LessonDt, 3, 4, 8f, LessonDt, LessonDt);
        return new GroupLessonsInput(null, [], [], [], [
            new ExistingLesson(9, values,
                [new ExistingLessonStudent(21, 10, 1, 1.5f, true), new ExistingLessonStudent(22, 11, 2, 1.5f, false)]),
            new ExistingLesson(10, values with { LessonDt = LessonDt.AddDays(7) },
                [new ExistingLessonStudent(31, 10, 1, 1.5f, false)])
        ]);
    }

    private static Dictionary<int, bool> EnteredData(GroupLessonsInput input)
    {
        return input.Lessons.SelectMany(l => l.Students).ToDictionary(s => s.Id, s => s.HasEnteredData);
    }

    //the rows of the request get the entered data they will be saved with; a row left out of the request and
    //the other lessons stay as they are
    [Fact]
    public void ToLessonsInput_RowsOfTheRequestGetTheirEnteredDataAfterTheSave()
    {
        // Arrange
        GroupLessonsInput current = SavedLessons();
        var request = new LessonRequest
        {
            LessonStatusId = 1, Students = [new LessonStudentRequest { Id = 22, TeacherComment = "late" }]
        };

        // Act
        GroupLessonsInput input = LessonMapper.ToLessonsInput(current, 9, request);

        // Assert
        Assert.Equal(new Dictionary<int, bool> { [21] = true, [22] = true, [31] = false }, EnteredData(input));
        Assert.Same(current.Lessons[1], input.Lessons[1]);
    }

    //blank texts are saved as NULL and the late minutes are no entered data (D65, D73)
    [Theory]
    [InlineData(false, null, null, " ", "", 5, false)]
    [InlineData(true, null, null, null, null, 0, true)]
    [InlineData(false, " theme ", null, null, null, 0, true)]
    [InlineData(false, null, 8f, null, null, 0, true)]
    [InlineData(false, null, null, null, "comment", 0, true)]
    public void ToLessonsInput_EnteredDataIsCountedAsTheRowWillBeSaved(bool present, string? theme, float? rate,
        string? teacherComment, string? studentComment, int studentLateMinutes, bool expected)
    {
        var request = new LessonRequest
        {
            LessonStatusId = 1,
            Students =
            [
                new LessonStudentRequest
                {
                    Id = 21,
                    Present = present,
                    Theme = theme,
                    Rate = rate,
                    TeacherComment = teacherComment,
                    StudentComment = studentComment,
                    StudentLateMinutes = studentLateMinutes
                }
            ]
        };

        GroupLessonsInput input = LessonMapper.ToLessonsInput(SavedLessons(), 9, request);

        Assert.Equal(expected, EnteredData(input)[21]);
    }
}
