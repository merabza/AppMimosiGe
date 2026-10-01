using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class LessonGeneratorMapperTests
{
    private static readonly DateTime Now = Date(10, 1, 9, 30);

    private readonly Mock<ILessonGeneratorRepository> _repository = new();

    //group 42 with two teachers, a student, two schedule rows (given out of their id order), lesson 100 with two
    //student rows and an old log entry
    private static Group Group()
    {
        var group = new Group { GrpId = 42, GroupCode = "1001", VoidDate = Date(12, 1), DirtyLessons = true };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 12, TeacherContractId = 6, SalarySchemaId = 9, StartDate = Date(9, 16)
        });
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 11, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Date(9, 1), EndDate = Date(9, 16)
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 1,
            StudentContractId = 10,
            HoursCoefficient = 0.5f,
            FourWeekHours = 12f,
            StartDate = Date(9, 1),
            EndDate = Date(10, 1)
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 32,
            WeekDayId = Wednesday,
            LessonStartTime = new LessonStartTime { LstTime = new TimeOnly(17, 30) },
            HoursCount = 2f,
            StartDate = Date(9, 1)
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 31,
            WeekDayId = Monday,
            LessonStartTime = new LessonStartTime { LstTime = new TimeOnly(15, 0) },
            HoursCount = 1.5f,
            StartDate = Date(9, 1),
            EndDate = Date(9, 15)
        });
        var lesson = new Lesson
        {
            Id = 100,
            GroupId = 42,
            LessonDt = Date(9, 7, 15),
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 12f,
            TeoMinDate = Date(9, 2, 17, 30),
            TeoMaxDate = Date(9, 30, 17, 30)
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 200, LessonId = 100, StudentContractId = 10, GroupByStudentId = 1, HoursCount = 0.75f
        });
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 201,
            LessonId = 100,
            StudentContractId = 11,
            GroupByStudentId = null,
            HoursCount = 1.5f,
            Present = true
        });
        group.Lessons.Add(lesson);
        group.LessonsCheckCreateErrorLogs.Add(new LessonCheckCreateErrorLog
        {
            Id = 5, GroupId = 42, ErrorLogTextId = 6, LessonDate = Date(9, 14)
        });
        return group;
    }

    private static LessonValues Values(DateTime lessonDt, int teacherContractId = 5)
    {
        return new LessonValues(lessonDt, teacherContractId, 8, 12f, Date(9, 2, 17, 30), Date(9, 30, 17, 30));
    }

    // --- ToInput

    [Fact]
    public void ToInput_MapsTheGroupRowsTeachersAndScheduleInTheirIdOrder()
    {
        GroupLessonsInput input = LessonGeneratorMapper.ToInput(Group());

        Assert.Equal(Date(12, 1), input.VoidDate);
        Assert.Equal([Teacher(5, 8, Date(9, 1), Date(9, 16)), Teacher(6, 9, Date(9, 16))], input.Teachers);
        Assert.Equal([Student(1, 10, Date(9, 1), Date(10, 1), 12f, 0.5f)], input.Students);
        Assert.Equal(
        [
            Schedule(Monday, 15, 0, 1.5f, Date(9, 1), Date(9, 15)), Schedule(Wednesday, 17, 30, 2f, Date(9, 1))
        ], input.DayTimePlaces);
    }

    [Fact]
    public void ToInput_MapsTheLessonsWithTheirStudentsAndEnteredData()
    {
        ExistingLesson lesson = Assert.Single(LessonGeneratorMapper.ToInput(Group()).Lessons);

        Assert.Equal(100, lesson.Id);
        Assert.Equal(Values(Date(9, 7, 15)), lesson.Values);
        Assert.Equal(
        [
            new ExistingLessonStudent(200, 10, 1, 0.75f, false), new ExistingLessonStudent(201, 11, null, 1.5f, true)
        ], lesson.Students);
    }

    // --- HasEnteredData

    [Theory]
    [InlineData(true, null, null, null, null, true)]
    [InlineData(false, "Theme", null, null, null, true)]
    [InlineData(false, "", null, null, null, true)]
    [InlineData(false, null, 5f, null, null, true)]
    [InlineData(false, null, null, "Teacher", null, true)]
    [InlineData(false, null, null, null, "Student", true)]
    [InlineData(false, null, null, null, null, false)]
    public void HasEnteredData_AnyAttendanceThemeRateOrComment(bool present, string? theme, float? rate,
        string? teacherComment, string? studentComment, bool expected)
    {
        var lessonStudent = new LessonByStudent
        {
            Present = present,
            Theme = theme,
            Rate = rate,
            TeacherComment = teacherComment,
            StudentComment = studentComment,
            StudentLateMinutes = 10
        };

        Assert.Equal(expected, LessonGeneratorMapper.HasEnteredData(lessonStudent));
    }

    // --- ApplyPlan

    private static GroupLessonsPlan PlanOf(IReadOnlyList<PlannedLessonChange> changes,
        IReadOnlyList<PlannedLogEntry>? logs = null, bool clearDirtyLessons = true)
    {
        return new GroupLessonsPlan(changes, logs ?? [], [], clearDirtyLessons, null);
    }

    [Fact]
    public void ApplyPlan_Create_AddsTheLessonWithItsStudentsAndReturnsIt()
    {
        // Arrange
        Group group = Group();
        Lesson? added = null;
        _repository.Setup(r => r.AddLesson(It.IsAny<Lesson>())).Callback<Lesson>(lesson => added = lesson);
        GroupLessonsPlan plan = PlanOf([
            new PlannedLessonChange(ELessonChangeKind.Create, null, Values(Date(9, 9, 17, 30), 6), null,
                [new PlannedStudentRow(EStudentRowChangeKind.Add, null, 10, 1, 1f)])
        ]);

        // Act
        Dictionary<DateTime, Lesson> created =
            LessonGeneratorMapper.ApplyPlan(group, plan, [], _repository.Object, Now);

        // Assert
        Assert.NotNull(added);
        Assert.Same(added, created[Date(9, 9, 17, 30)]);
        Assert.Equal((42, Date(9, 9, 17, 30), 6, 8, 12f, Date(9, 2, 17, 30), Date(9, 30, 17, 30), 1),
            (added.GroupId, added.LessonDt, added.TeacherContractId, added.SalarySchemaId, added.FourWeekHours,
                added.TeoMinDate, added.TeoMaxDate, added.LessonStatusId));
        LessonByStudent student = Assert.Single(added.LessonsByStudents);
        Assert.Equal((10, (int?)1, 1f, false), (student.StudentContractId, student.GroupByStudentId,
            student.HoursCount, student.Present));
    }

    [Fact]
    public void ApplyPlan_Update_SetsTheLessonFieldsAndChangesItsStudentRows()
    {
        // Arrange
        Group group = Group();
        Lesson lesson = group.Lessons.Single();
        GroupLessonsPlan plan = PlanOf([
            new PlannedLessonChange(ELessonChangeKind.Update, 100, Values(Date(9, 7, 16), 6), Values(Date(9, 7, 15)),
            [
                new PlannedStudentRow(EStudentRowChangeKind.Update, 200, 10, 1, 1f),
                new PlannedStudentRow(EStudentRowChangeKind.Delete, 201, 11, null, 1.5f),
                new PlannedStudentRow(EStudentRowChangeKind.Add, null, 12, 3, 2f)
            ])
        ]);

        // Act
        Dictionary<DateTime, Lesson> created =
            LessonGeneratorMapper.ApplyPlan(group, plan, [], _repository.Object, Now);

        // Assert
        Assert.Empty(created);
        Assert.Equal((Date(9, 7, 16), 6), (lesson.LessonDt, lesson.TeacherContractId));
        LessonByStudent updated = lesson.LessonsByStudents.Single(s => s.Id == 200);
        Assert.Equal(((int?)1, 1f), (updated.GroupByStudentId, updated.HoursCount));
        _repository.Verify(r => r.RemoveLessonStudent(It.Is<LessonByStudent>(s => s.Id == 201)), Times.Once);
        LessonByStudent addedRow = Assert.Single(lesson.LessonsByStudents, s => s.Id == 0);
        Assert.Equal((12, (int?)3, 2f), (addedRow.StudentContractId, addedRow.GroupByStudentId, addedRow.HoursCount));
        _repository.Verify(r => r.AddLesson(It.IsAny<Lesson>()), Times.Never);
    }

    [Fact]
    public void ApplyPlan_Delete_RemovesTheLesson()
    {
        Group group = Group();

        LessonGeneratorMapper.ApplyPlan(group,
            PlanOf([new PlannedLessonChange(ELessonChangeKind.Delete, 100, Values(Date(9, 7, 15)), null, [])]), [],
            _repository.Object, Now);

        _repository.Verify(r => r.RemoveLesson(It.Is<Lesson>(l => l.Id == 100)), Times.Once);
    }

    [Fact]
    public void ApplyPlan_ReplacesTheGroupLogWithThePlanLog()
    {
        // Arrange
        Group group = Group();
        List<LessonCheckCreateErrorLog> logs = [];
        _repository.Setup(r => r.ReplaceLogs(group, It.IsAny<IEnumerable<LessonCheckCreateErrorLog>>()))
            .Callback<Group, IEnumerable<LessonCheckCreateErrorLog>>((_, newLogs) => logs.AddRange(newLogs));

        // Act
        LessonGeneratorMapper.ApplyPlan(group,
            PlanOf([], [new PlannedLogEntry(11, Date(9, 8, 15), 100), new PlannedLogEntry(1, null, null)]), [],
            _repository.Object, Now);

        // Assert
        Assert.Equal([(Now, 42, 11, Date(9, 8, 15), 100), (Now, 42, 1, (DateTime?)null, (int?)null)],
            logs.Select(l => (l.CreatedDate, l.GroupId, l.ErrorLogTextId, l.LessonDate, l.LessonId)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ApplyPlan_ClearsDirtyLessonsOnlyWhenThePlanSaysSo(bool clearDirtyLessons, bool expectedDirtyLessons)
    {
        Group group = Group();

        LessonGeneratorMapper.ApplyPlan(group, PlanOf([], clearDirtyLessons: clearDirtyLessons), [],
            _repository.Object, Now);

        Assert.Equal(expectedDirtyLessons, group.DirtyLessons);
    }

    [Fact]
    public void ApplyPlan_SetsDirtyNextPayDateOfTheGivenContracts()
    {
        List<StudentContract> contracts =
        [
            new() { ScId = 10, ContractNumber = "6.001", DirtyNextPayDate = false },
            new() { ScId = 11, ContractNumber = "6.002", DirtyNextPayDate = false }
        ];

        LessonGeneratorMapper.ApplyPlan(Group(), PlanOf([]), contracts, _repository.Object, Now);

        Assert.All(contracts, c => Assert.True(c.DirtyNextPayDate));
    }

    // --- ToResponse

    [Fact]
    public void ToResponse_CountsTheChangesAndMapsTheErrorTexts()
    {
        // Arrange
        GroupLessonsPlan plan = new([
            new PlannedLessonChange(ELessonChangeKind.Create, null, Values(Date(9, 9, 17, 30)), null,
                [new PlannedStudentRow(EStudentRowChangeKind.Add, null, 10, 1, 1f)]),
            new PlannedLessonChange(ELessonChangeKind.Update, 100, Values(Date(9, 7, 16)), Values(Date(9, 7, 15)),
                [new PlannedStudentRow(EStudentRowChangeKind.Add, null, 12, 3, 2f)]),
            new PlannedLessonChange(ELessonChangeKind.Update, 101, Values(Date(9, 14, 15)), null,
            [
                new PlannedStudentRow(EStudentRowChangeKind.Update, 202, 10, 1, 1f),
                new PlannedStudentRow(EStudentRowChangeKind.Delete, 203, 11, null, 1f)
            ]),
            new PlannedLessonChange(ELessonChangeKind.Delete, 102, Values(Date(9, 15, 15)), null,
                [new PlannedStudentRow(EStudentRowChangeKind.Delete, 204, 10, 1, 1f)])
        ], [new PlannedLogEntry(6, Date(9, 21), null), new PlannedLogEntry(99, Date(9, 22), 7)], [10, 11, 12], true,
            null);

        // Act
        GroupLessonsGenerationResponse response = LessonGeneratorMapper.ToResponse(Group(), plan,
            new Dictionary<int, string> { [6] = "no teacher" }, new Dictionary<DateTime, Lesson>());

        // Assert
        Assert.Equal((42, "1001", 1, 1, 1, 1, 1, 1, 3),
            (response.GrpId, response.GroupCode, response.CreatedLessonsCount, response.UpdatedLessonsCount,
                response.DeletedLessonsCount, response.AddedStudentsCount, response.UpdatedStudentsCount,
                response.DeletedStudentsCount, response.DirtyStudentContractsCount));
        Assert.Equal(
        [
            new LessonGeneratorErrorResponse(6, "no teacher", Date(9, 21), null),
            new LessonGeneratorErrorResponse(99, "", Date(9, 22), 7)
        ], response.Errors);
        Assert.Equal(["create", "update", "update", "delete"], response.Changes.Select(c => c.Action));
        Assert.Equal([null, 100, 101, 102], response.Changes.Select(c => c.LessonId));
    }

    [Fact]
    public void ToResponse_UpdatedLesson_ListsTheChangedFieldsAndThePreviousTime()
    {
        GroupLessonsPlan plan = new([
            new PlannedLessonChange(ELessonChangeKind.Update, 100,
                new LessonValues(Date(9, 7, 16), 6, 9, 8f, Date(9, 1), Date(9, 29)), Values(Date(9, 7, 15)), []),
            new PlannedLessonChange(ELessonChangeKind.Update, 101, Values(Date(9, 14, 15), 6),
                Values(Date(9, 14, 15)), [])
        ], [], [], true, null);

        GroupLessonsGenerationResponse response = LessonGeneratorMapper.ToResponse(Group(), plan,
            new Dictionary<int, string>(), new Dictionary<DateTime, Lesson>());

        LessonChangeResponse moved = response.Changes[0];
        Assert.Equal(Date(9, 7, 15), moved.PreviousLessonDt);
        Assert.Equal(
            ["lessonDt", "teacherContractId", "salarySchemaId", "fourWeekHours", "teoMinDate", "teoMaxDate"],
            moved.ChangedFields);
        LessonChangeResponse sameTime = response.Changes[1];
        Assert.Null(sameTime.PreviousLessonDt);
        Assert.Equal(["teacherContractId"], sameTime.ChangedFields);
    }

    [Fact]
    public void ToResponse_CreatedAndDeletedLessons_CountNoStudentRowsAndNoFields()
    {
        GroupLessonsPlan plan = new([
            new PlannedLessonChange(ELessonChangeKind.Create, null, Values(Date(9, 9, 17, 30)), null,
                [new PlannedStudentRow(EStudentRowChangeKind.Add, null, 10, 1, 1f)]),
            new PlannedLessonChange(ELessonChangeKind.Delete, 102, Values(Date(9, 15, 15)), null,
                [new PlannedStudentRow(EStudentRowChangeKind.Delete, 204, 10, 1, 1f)])
        ], [], [], true, null);

        GroupLessonsGenerationResponse response = LessonGeneratorMapper.ToResponse(Group(), plan,
            new Dictionary<int, string>(), new Dictionary<DateTime, Lesson>());

        Assert.All(response.Changes, c =>
        {
            Assert.Empty(c.ChangedFields);
            Assert.Equal((0, 0, 0), (c.AddedStudentsCount, c.UpdatedStudentsCount, c.DeletedStudentsCount));
        });
        Assert.Equal((0, 0, 0),
            (response.AddedStudentsCount, response.UpdatedStudentsCount, response.DeletedStudentsCount));
    }

    [Fact]
    public void ToResponse_SavedNewLesson_GetsItsId()
    {
        GroupLessonsPlan plan = new([
            new PlannedLessonChange(ELessonChangeKind.Create, null, Values(Date(9, 9, 17, 30)), null, [])
        ], [], [], true, null);

        GroupLessonsGenerationResponse response = LessonGeneratorMapper.ToResponse(Group(), plan,
            new Dictionary<int, string>(),
            new Dictionary<DateTime, Lesson> { [Date(9, 9, 17, 30)] = new() { Id = 555 } });

        Assert.Equal(555, Assert.Single(response.Changes).LessonId);
    }
}
