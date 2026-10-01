using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class GroupLessonsGenerationTests
{
    private static readonly DateTime Now = Date(10, 1, 9, 30);

    private readonly Mock<ILessonGeneratorRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public GroupLessonsGenerationTests()
    {
        _repository.Setup(r => r.GetErrorLogTexts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [6] = "no teacher" });
        _repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            [
                .. ids.Select(id => new StudentContract { ScId = id, ContractNumber = "6.001" })
            ]);
    }

    //group 42: a Monday 15:00 group of September with one student (contract 10) and no lessons yet
    private static Group Group()
    {
        var group = new Group { GrpId = 42, GroupCode = "1001", DirtyLessons = true };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 1, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Date(9, 1)
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 1, StudentContractId = 10, FourWeekHours = 8f, StartDate = Date(9, 1)
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 1,
            WeekDayId = Monday,
            LessonStartTime = new LessonStartTime { LstTime = new TimeOnly(15, 0) },
            HoursCount = 1.5f,
            StartDate = Date(9, 1)
        });
        return group;
    }

    // --- PrepareOperationMonths

    [Fact]
    public async Task PrepareOperationMonths_MonthsMissing_AddsThemMarksEverythingDirtyAndSaves()
    {
        // Arrange
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>())).ReturnsAsync(Date(10, 1));
        List<DateTime> added = [];
        _repository.Setup(r => r.AddOperationMonths(It.IsAny<IEnumerable<DateTime>>()))
            .Callback<IEnumerable<DateTime>>(months => added.AddRange(months));

        // Act
        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(_repository.Object,
            _unitOfWork.Object, Date(10, 15), false, CancellationToken.None);

        // Assert
        Assert.Equal(new GenerationHorizon(Date(12, 31), 2), horizon);
        Assert.Equal([Date(11, 1), Date(12, 1)], added);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareOperationMonths_DryRun_WritesNothingButCountsTheHorizonWithTheMissingMonths()
    {
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>())).ReturnsAsync(Date(10, 1));

        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(_repository.Object,
            _unitOfWork.Object, Date(10, 15), true, CancellationToken.None);

        Assert.Equal(new GenerationHorizon(Date(12, 31), 2), horizon);
        _repository.Verify(r => r.AddOperationMonths(It.IsAny<IEnumerable<DateTime>>()), Times.Never);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareOperationMonths_CalendarLongEnough_WritesNothingAndUsesItsLastMonth()
    {
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2027, 11, 1, 0, 0, 0, DateTimeKind.Unspecified));

        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(_repository.Object,
            _unitOfWork.Object, Now, false, CancellationToken.None);

        Assert.Equal(new GenerationHorizon(new DateTime(2027, 11, 30, 0, 0, 0, DateTimeKind.Unspecified), 0), horizon);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Run

    [Fact]
    public async Task Run_GroupNotFound_ReturnsNull()
    {
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            false, Now, input => GroupLessonsPlanner.PlanGroup(input, SeptemberEnd), CancellationToken.None);

        Assert.Null(result);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_DryRun_LoadsWithoutTrackingAndWritesNothing()
    {
        // Arrange
        _repository.Setup(r => r.GetGroupForGeneration(42, false, It.IsAny<CancellationToken>())).ReturnsAsync(Group());

        // Act
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            true, Now, input => GroupLessonsPlanner.PlanGroup(input, SeptemberEnd), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Response.CreatedLessonsCount);
        Assert.All(result.Response.Changes, c => Assert.Null(c.LessonId));
        _repository.Verify(r => r.AddLesson(It.IsAny<Lesson>()), Times.Never);
        _repository.Verify(
            r => r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_AppliesThePlanSavesOnceAndReturnsTheSavedIds()
    {
        // Arrange
        Group group = Group();
        _repository.Setup(r => r.GetGroupForGeneration(42, true, It.IsAny<CancellationToken>())).ReturnsAsync(group);
        List<Lesson> added = [];
        _repository.Setup(r => r.AddLesson(It.IsAny<Lesson>())).Callback<Lesson>(added.Add);
        //the database gives the new lessons their ids
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() =>
        {
            for (int i = 0; i < added.Count; i++)
            {
                added[i].Id = 500 + i;
            }
        });

        // Act
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            false, Now, input => GroupLessonsPlanner.PlanGroup(input, SeptemberEnd), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, added.Count);
        Assert.Equal([500, 501, 502, 503], result.Response.Changes.Select(c => c.LessonId!.Value));
        Assert.False(group.DirtyLessons);
        Assert.Equal(1, result.Response.DirtyStudentContractsCount);
        _repository.Verify(
            r => r.GetStudentContractsForChange(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(10)),
                It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(result.LastLesson);
        Assert.Null(result.LastLessonId);
    }

    [Fact]
    public async Task Run_LastLessonCreated_ReturnsItsSavedId()
    {
        // Arrange
        _repository.Setup(r => r.GetGroupForGeneration(42, true, It.IsAny<CancellationToken>())).ReturnsAsync(Group());
        Lesson? added = null;
        _repository.Setup(r => r.AddLesson(It.IsAny<Lesson>())).Callback<Lesson>(lesson => added = lesson);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => added!.Id = 777);

        // Act
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            false, Now, input => GroupLessonsPlanner.PlanLastLesson(input, SeptemberEnd, Date(9, 17)),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new PlannedLastLesson(null, Date(9, 14, 15)), result.LastLesson);
        Assert.Equal(777, result.LastLessonId);
    }

    [Fact]
    public async Task Run_LastLessonExists_ReturnsItsId()
    {
        // Arrange
        Group group = Group();
        var lesson = new Lesson
        {
            Id = 100,
            LessonDt = Date(9, 14, 15),
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 8f,
            TeoMinDate = Date(9, 7, 15),
            TeoMaxDate = Date(9, 28, 15)
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 200, StudentContractId = 10, GroupByStudentId = 1, HoursCount = 1.5f
        });
        group.Lessons.Add(lesson);
        _repository.Setup(r => r.GetGroupForGeneration(42, true, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        // Act
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            false, Now, input => GroupLessonsPlanner.PlanLastLesson(input, SeptemberEnd, Date(9, 17)),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(100, result.LastLessonId);
        Assert.Empty(result.Response.Changes);
    }

    [Fact]
    public async Task Run_MapsTheErrorTexts()
    {
        //no teacher between the 10th and the 20th
        Group group = Group();
        group.GroupsByTeachers.Single().EndDate = Date(9, 10);
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 2, TeacherContractId = 6, SalarySchemaId = 8, StartDate = Date(9, 20)
        });
        _repository.Setup(r => r.GetGroupForGeneration(42, false, It.IsAny<CancellationToken>())).ReturnsAsync(group);

        GroupGenerationResult? result = await GroupLessonsGeneration.Run(_repository.Object, _unitOfWork.Object, 42,
            true, Now, input => GroupLessonsPlanner.PlanGroup(input, SeptemberEnd), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal([new LessonGeneratorErrorResponse(6, "no teacher", Date(9, 14), null)], result.Response.Errors);
    }
}
