using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGe.Application.LessonGenerator.GetLessonGeneratorLog;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.LessonGenerator.LessonGeneratorTestData;

namespace AppMimosiGe.Application.Tests.LessonGenerator;

public sealed class LessonGeneratorHandlersTests
{
    //1 October 2026, 9:30 local time
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 30, 0, TimeSpan.FromHours(4));

    private readonly Mock<ILessonGeneratorRepository> _repository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public LessonGeneratorHandlersTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
        _timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.CreateCustomTimeZone("Georgia",
            TimeSpan.FromHours(4), "Georgia", "Georgia"));
        SetUp(_repository, null);
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified));
    }

    private static void SetUp(Mock<ILessonGeneratorRepository> repository, Group? group)
    {
        repository.Setup(r => r.GetErrorLogTexts(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string>());
        repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        if (group is not null)
        {
            repository.Setup(r => r.GetGroupForGeneration(group.GrpId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(group);
        }
    }

    //a Monday 15:00 group from 1 September with one student
    private static Group Group(int grpId = 42)
    {
        var group = new Group { GrpId = grpId, GroupCode = grpId.ToString(CultureInfo.InvariantCulture) };
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

    // --- one group

    [Fact]
    public async Task GenerateGroupLessons_AddsTheMissingMonthsAndGeneratesUpToTheirEnd()
    {
        // Arrange
        SetUp(_repository, Group());
        var handler = new GenerateGroupLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            _timeProvider.Object);

        // Act
        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupLessonsCommand(42, false), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        //1 October + 2 months: October, November and December are added after September
        Assert.Equal((false, Date(12, 31), 3),
            (result.Value.DryRun, result.Value.HorizonEnd, result.Value.AddedOperationMonthsCount));
        GroupLessonsGenerationResponse group = Assert.Single(result.Value.Groups);
        //the Mondays of September to December
        Assert.Equal(17, group.CreatedLessonsCount);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetGroupForGeneration(42, true, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateGroupLessons_DryRun_WritesNothing()
    {
        SetUp(_repository, Group());
        var handler = new GenerateGroupLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupLessonsCommand(42, true), CancellationToken.None);

        Assert.True(result.Value.DryRun);
        Assert.Equal(17, Assert.Single(result.Value.Groups).CreatedLessonsCount);
        _repository.Verify(r => r.GetGroupForGeneration(42, false, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateGroupLessons_GroupNotFound_ReturnsNotFound()
    {
        var handler = new GenerateGroupLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupLessonsCommand(42, false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(GroupErrors.GroupNotFound.Code, result.Error.Code);
    }

    // --- the last lesson

    [Fact]
    public async Task GenerateGroupLastLesson_ReturnsTheLastLessonUpToToday()
    {
        // Arrange
        SetUp(_repository, Group());
        Lesson? added = null;
        _repository.Setup(r => r.AddLesson(It.IsAny<Lesson>())).Callback<Lesson>(lesson => added = lesson);
        //the first save adds the missing months, the second one the lesson
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => added?.Id = 900);
        var handler = new GenerateGroupLastLessonCommandHandler(_repository.Object, _unitOfWork.Object,
            _timeProvider.Object);

        // Act
        Result<GroupLastLessonResponse> result =
            await handler.Handle(new GenerateGroupLastLessonCommand(42), CancellationToken.None);

        // Assert: the last Monday before 1 October is 28 September
        Assert.True(result.IsSuccess);
        Assert.Equal((900, Date(9, 28, 15)), (result.Value.LessonId, result.Value.LessonDt));
        Assert.False(result.Value.Generation.DryRun);
        Assert.Equal(1, Assert.Single(result.Value.Generation.Groups).CreatedLessonsCount);
        //the last lesson is no dry run: the missing months are saved first
        _repository.Verify(r => r.AddOperationMonths(It.IsAny<IEnumerable<DateTime>>()), Times.Once);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateGroupLastLesson_GroupNotFound_ReturnsNotFound()
    {
        var handler = new GenerateGroupLastLessonCommandHandler(_repository.Object, _unitOfWork.Object,
            _timeProvider.Object);

        Result<GroupLastLessonResponse> result =
            await handler.Handle(new GenerateGroupLastLessonCommand(42), CancellationToken.None);

        Assert.Equal(GroupErrors.GroupNotFound.Code, result.Error.Code);
    }

    // --- dirty and all groups

    //every group runs in its own scope: its own repository, unit of work and transaction
    private (Mock<IServiceScopeFactory> ScopeFactory, List<Mock<IUnitOfWork>> GroupUnitsOfWork) Scopes(
        params Group?[] groups)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        List<Mock<IUnitOfWork>> unitsOfWork = [];
        var scopes = new Queue<IServiceScope>(groups.Select(group =>
        {
            var groupRepository = new Mock<ILessonGeneratorRepository>();
            SetUp(groupRepository, group);
            var groupUnitOfWork = new Mock<IUnitOfWork>();
            unitsOfWork.Add(groupUnitOfWork);
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(ILessonGeneratorRepository))).Returns(groupRepository.Object);
            provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(groupUnitOfWork.Object);
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            return scope.Object;
        }));
        scopeFactory.Setup(f => f.CreateScope()).Returns(scopes.Dequeue);
        return (scopeFactory, unitsOfWork);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateGroupsLessons_GeneratesEveryGroupInItsOwnScope(bool onlyDirty)
    {
        // Arrange
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified));
        _repository.Setup(r => r.GetGroupIds(onlyDirty, It.IsAny<CancellationToken>())).ReturnsAsync([41, 42]);
        (Mock<IServiceScopeFactory> scopeFactory, List<Mock<IUnitOfWork>> groupUnitsOfWork) =
            Scopes(Group(41), Group());
        var handler = new GenerateGroupsLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            scopeFactory.Object, _timeProvider.Object);

        // Act
        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(onlyDirty, false), CancellationToken.None);

        // Assert
        Assert.Equal([41, 42], result.Value.Groups.Select(g => g.GrpId));
        Assert.All(groupUnitsOfWork, u => u.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal((Date(12, 31), 0), (result.Value.HorizonEnd, result.Value.AddedOperationMonthsCount));
    }

    //a new month would make every group dirty: the dry run checks all of them
    [Fact]
    public async Task GenerateGroupsLessons_DryRunWithMissingMonths_ChecksAllGroups()
    {
        _repository.Setup(r => r.GetGroupIds(false, It.IsAny<CancellationToken>())).ReturnsAsync([42]);
        (Mock<IServiceScopeFactory> scopeFactory, List<Mock<IUnitOfWork>> groupUnitsOfWork) = Scopes(Group());
        var handler = new GenerateGroupsLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            scopeFactory.Object, _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(true, true), CancellationToken.None);

        Assert.Equal((true, 3), (result.Value.DryRun, result.Value.AddedOperationMonthsCount));
        Assert.Single(result.Value.Groups);
        _repository.Verify(r => r.GetGroupIds(true, It.IsAny<CancellationToken>()), Times.Never);
        groupUnitsOfWork[0].Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    //without a new month the dry run of the dirty groups checks only them
    [Fact]
    public async Task GenerateGroupsLessons_DryRunWithoutMissingMonths_ChecksOnlyTheDirtyGroups()
    {
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified));
        _repository.Setup(r => r.GetGroupIds(true, It.IsAny<CancellationToken>())).ReturnsAsync([42]);
        var handler = new GenerateGroupsLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            Scopes(Group()).ScopeFactory.Object, _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(true, true), CancellationToken.None);

        Assert.Equal([42], result.Value.Groups.Select(g => g.GrpId));
        _repository.Verify(r => r.GetGroupIds(false, It.IsAny<CancellationToken>()), Times.Never);
    }

    //the months were added and saved, so the dirty groups are read after that
    [Fact]
    public async Task GenerateGroupsLessons_MissingMonths_AreAddedBeforeTheDirtyGroupsAreRead()
    {
        _repository.Setup(r => r.GetGroupIds(true, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var handler = new GenerateGroupsLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            Scopes().ScopeFactory.Object, _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(true, false), CancellationToken.None);

        Assert.Empty(result.Value.Groups);
        _repository.Verify(r => r.MarkAllDirty(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateGroupsLessons_GroupDeletedMeanwhile_IsSkipped()
    {
        _repository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified));
        _repository.Setup(r => r.GetGroupIds(false, It.IsAny<CancellationToken>())).ReturnsAsync([41, 42]);
        var handler = new GenerateGroupsLessonsCommandHandler(_repository.Object, _unitOfWork.Object,
            Scopes(null, Group()).ScopeFactory.Object, _timeProvider.Object);

        Result<LessonsGenerationResponse> result =
            await handler.Handle(new GenerateGroupsLessonsCommand(false, false), CancellationToken.None);

        Assert.Equal([42], result.Value.Groups.Select(g => g.GrpId));
    }

    // --- log

    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    public async Task GetLessonGeneratorLog_ReturnsTheRepositoryRows(int? grpId)
    {
        List<LessonGeneratorLogRowResponse> rows = [new(1, Date(10, 1), 42, "1001", 6, "text", Date(10, 5), null)];
        _repository.Setup(r => r.GetLog(grpId, It.IsAny<CancellationToken>())).ReturnsAsync(rows);
        var handler = new GetLessonGeneratorLogQueryHandler(_repository.Object);

        Result<List<LessonGeneratorLogRowResponse>> result =
            await handler.Handle(new GetLessonGeneratorLogQuery(grpId), CancellationToken.None);

        Assert.Same(rows, result.Value);
    }
}
