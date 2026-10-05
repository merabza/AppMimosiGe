using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears;
using AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;
using AppMimosiGe.Application.AcademicYears.CreateAcademicYear;
using AppMimosiGe.Application.AcademicYears.GetAcademicYears;
using AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.AcademicYears;

public sealed class AcademicYearHandlersTests
{
    //5 October 2026, 12:00 local time: the current year is 2026-2027
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.FromHours(4));

    private readonly Mock<IAcademicYearsRepository> _repository = new();
    private readonly Mock<ILessonGeneratorRepository> _lessonGeneratorRepository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public AcademicYearHandlersTests()
    {
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(Now);
        _timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.CreateCustomTimeZone("Georgia",
            TimeSpan.FromHours(4), "Georgia", "Georgia"));
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Year(10, 2025), Year(11, 2026)]);
        //the calendar already reaches November 2027: no month is added
        _lessonGeneratorRepository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Date(11, 1, 2027));
    }

    private static DateTime Date(int month, int day, int year, int hour = 0) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static AcademicYear Year(int ayId, int startYear) => new()
    {
        AyId = ayId,
        AcademicYearName = $"{startYear}-{startYear + 1}",
        StartDate = Date(9, 1, startYear),
        FinishDate = Date(9, 1, startYear + 1)
    };

    // --- step 1: the new year

    private CreateAcademicYearCommandHandler CreateHandler() =>
        new(_repository.Object, _unitOfWork.Object, _timeProvider.Object);

    [Fact]
    public async Task Create_DryRun_PlansTheYearAfterTheCurrentOneWithoutSaving()
    {
        Result<NewAcademicYearResponse> result =
            await CreateHandler().Handle(new CreateAcademicYearCommand(true), CancellationToken.None);

        Assert.Equal(new NewAcademicYearResponse(true, null, "2027-2028", Date(9, 1, 2027), Date(9, 1, 2028), "7",
            false), result.Value);
        _repository.Verify(r => r.Add(It.IsAny<AcademicYear>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_AddsTheYearOfThePreview()
    {
        // Arrange
        AcademicYear? added = null;
        _repository.Setup(r => r.Add(It.IsAny<AcademicYear>())).Callback<AcademicYear>(ay => added = ay);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => added!.AyId = 12);

        // Act
        Result<NewAcademicYearResponse> preview =
            await CreateHandler().Handle(new CreateAcademicYearCommand(true), CancellationToken.None);
        Result<NewAcademicYearResponse> result =
            await CreateHandler().Handle(new CreateAcademicYearCommand(false), CancellationToken.None);

        // Assert
        Assert.Equal(preview.Value with { DryRun = false, AyId = 12 }, result.Value);
        Assert.Equal(("2027-2028", Date(9, 1, 2027), Date(9, 1, 2028)),
            (added!.AcademicYearName, added.StartDate, added.FinishDate));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // the second run finds the year it added: the preview says so and the step is refused (idempotency)
    [Fact]
    public async Task Create_SecondRun_ShowsTheYearExistsAndAddsNothing()
    {
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Year(11, 2026), Year(12, 2027)]);

        Result<NewAcademicYearResponse> preview =
            await CreateHandler().Handle(new CreateAcademicYearCommand(true), CancellationToken.None);
        Result<NewAcademicYearResponse> result =
            await CreateHandler().Handle(new CreateAcademicYearCommand(false), CancellationToken.None);

        Assert.True(preview.Value.AlreadyExists);
        Assert.Equal(AcademicYearErrors.AcademicYearAlreadyExists.Code, result.Error.Code);
        _repository.Verify(r => r.Add(It.IsAny<AcademicYear>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithoutYears_ReturnsTheError()
    {
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Result<NewAcademicYearResponse> result =
            await CreateHandler().Handle(new CreateAcademicYearCommand(true), CancellationToken.None);

        Assert.Equal(AcademicYearErrors.NoAcademicYears.Code, result.Error.Code);
    }

    // --- step 2: closing the groups of a year

    //a Monday 15:00 group from 23 August 2027 (two lessons before the close date) with existing lessons after it: 6
    //September without data (deleted) and 13 September with an attendance (kept, error 11)
    private static Group GroupWithLessonsAfterTheYear(int grpId)
    {
        var group = new Group { GrpId = grpId, GroupCode = $"G{grpId}", DirtyLessons = false };
        group.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 1, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Date(8, 23, 2027)
        });
        group.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 1, StudentContractId = 10, FourWeekHours = 8f, FourWeekFee = 48m, StartDate = Date(8, 23, 2027)
        });
        group.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 1,
            WeekDayId = 1,
            LessonStartTime = new LessonStartTime { LstTime = new TimeOnly(15, 0) },
            HoursCount = 1.5f,
            StartDate = Date(8, 23, 2027)
        });
        group.Lessons.Add(ExistingLesson(700, Date(9, 6, 2027, 15), false));
        group.Lessons.Add(ExistingLesson(701, Date(9, 13, 2027, 15), true));
        return group;
    }

    private static Lesson ExistingLesson(int id, DateTime lessonDt, bool present)
    {
        var lesson = new Lesson
        {
            Id = id,
            LessonDt = lessonDt,
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 1.5f,
            LessonStatusId = 1,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = id + 100, LessonId = id, StudentContractId = 10, GroupByStudentId = 1, HoursCount = 1.5f, Present = present
        });
        return lesson;
    }

    //every group runs in its own scope: its own repository, unit of work and transaction
    private static (Mock<IServiceScopeFactory> ScopeFactory, List<Mock<IUnitOfWork>> UnitsOfWork) Scopes(
        params Group[] groups)
    {
        var scopeFactory = new Mock<IServiceScopeFactory>();
        List<Mock<IUnitOfWork>> unitsOfWork = [];
        var scopes = new Queue<IServiceScope>(groups.Select(group =>
        {
            var groupRepository = new Mock<ILessonGeneratorRepository>();
            groupRepository.Setup(r => r.GetGroupForGeneration(group.GrpId, It.IsAny<bool>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(group);
            groupRepository.Setup(r => r.GetErrorLogTexts(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<int, string>());
            groupRepository.Setup(r => r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>())).ReturnsAsync([]);
            var unitOfWork = new Mock<IUnitOfWork>();
            unitsOfWork.Add(unitOfWork);
            var provider = new Mock<IServiceProvider>();
            provider.Setup(p => p.GetService(typeof(ILessonGeneratorRepository))).Returns(groupRepository.Object);
            provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(unitOfWork.Object);
            var scope = new Mock<IServiceScope>();
            scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
            return scope.Object;
        }));
        scopeFactory.Setup(f => f.CreateScope()).Returns(scopes.Dequeue);
        return (scopeFactory, unitsOfWork);
    }

    private CloseAcademicYearGroupsCommandHandler CloseHandler(IServiceScopeFactory scopeFactory) =>
        new(_repository.Object, _lessonGeneratorRepository.Object, _unitOfWork.Object, scopeFactory,
            _timeProvider.Object);

    private void SetUpGroupsToClose(DateTime closeDate, params int[] grpIds)
    {
        _repository.Setup(r => r.GetGroupsToClose(11, closeDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. grpIds.Select(id => new GroupToClose(id, $"G{id}", "English"))]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CloseGroups_VoidsEachGroupAndDeletesItsLessonsAfterTheCloseDate(bool dryRun)
    {
        // Arrange: no close date: the end of the year
        Group group = GroupWithLessonsAfterTheYear(41);
        SetUpGroupsToClose(Date(9, 1, 2027), 41);
        (Mock<IServiceScopeFactory> scopeFactory, List<Mock<IUnitOfWork>> unitsOfWork) = Scopes(group);

        // Act
        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(scopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(11, null, dryRun), CancellationToken.None);

        // Assert
        CloseAcademicYearGroupsResponse response = result.Value;
        Assert.Equal((dryRun, 11, Date(9, 1, 2027), Date(11, 30, 2027)),
            (response.DryRun, response.AcademicYearId, response.CloseDate, response.HorizonEnd));
        CloseGroupResponse closed = Assert.Single(response.Groups);
        Assert.Equal((41, "G41", "English", (DateTime?)null, 1, 1, 1),
            (closed.GrpId, closed.GroupCode, closed.CourseName, closed.PreviousVoidDate, closed.OpenTeacherRowsCount,
                closed.OpenStudentRowsCount, closed.OpenScheduleRowsCount));
        //23 and 30 August are created, 6 September is deleted (-48 / 8 × 1.5), 13 September stays with error 11
        Assert.Equal((2, 1, -9m), (closed.Generation.CreatedLessonsCount, closed.Generation.DeletedLessonsCount,
            closed.DeletedLessonsCharges));
        LessonGeneratorErrorResponse error = Assert.Single(closed.Generation.Errors);
        Assert.Equal((LessonGeneratorErrorCodes.ExtraLessonHasEnteredData, 701), (error.ErrorCode, error.LessonId));
        Assert.Equal(Date(9, 1, 2027), group.VoidDate);
        unitsOfWork[0].Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            dryRun ? Times.Never : Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseGroups_ClosesEveryGroupOfTheYearInItsOwnScope()
    {
        DateTime closeDate = Date(7, 1, 2027);
        SetUpGroupsToClose(closeDate, 41, 42);
        (Mock<IServiceScopeFactory> scopeFactory, List<Mock<IUnitOfWork>> unitsOfWork) =
            Scopes(GroupWithLessonsAfterTheYear(41), GroupWithLessonsAfterTheYear(42));

        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(scopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(11, closeDate.AddHours(10), false), CancellationToken.None);

        Assert.Equal(closeDate, result.Value.CloseDate);
        Assert.Equal([41, 42], result.Value.Groups.Select(g => g.GrpId));
        Assert.All(unitsOfWork, u => u.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once));
    }

    // the repository returns only the groups still open on the close date: a second run closes nothing
    [Fact]
    public async Task CloseGroups_NothingOpen_ClosesNothing()
    {
        SetUpGroupsToClose(Date(9, 1, 2027));
        (Mock<IServiceScopeFactory> scopeFactory, _) = Scopes();

        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(scopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(11, null, false), CancellationToken.None);

        Assert.Empty(result.Value.Groups);
        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }

    // a group deleted in the meantime is left out
    [Fact]
    public async Task CloseGroups_MissingGroup_IsLeftOut()
    {
        SetUpGroupsToClose(Date(9, 1, 2027), 41);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(ILessonGeneratorRepository)))
            .Returns(new Mock<ILessonGeneratorRepository>().Object);
        provider.Setup(p => p.GetService(typeof(IUnitOfWork))).Returns(new Mock<IUnitOfWork>().Object);
        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(provider.Object);
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(scopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(11, null, false), CancellationToken.None);

        Assert.Empty(result.Value.Groups);
    }

    [Fact]
    public async Task CloseGroups_UnknownYear_IsNotFound()
    {
        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(Scopes().ScopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(99, null, true), CancellationToken.None);

        Assert.Equal(AcademicYearErrors.AcademicYearNotFound.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(9, 1, 2026)]
    [InlineData(8, 31, 2026)]
    public async Task CloseGroups_CloseDateNotAfterTheStart_IsRefused(int month, int day, int year)
    {
        Result<CloseAcademicYearGroupsResponse> result = await CloseHandler(Scopes().ScopeFactory.Object)
            .Handle(new CloseAcademicYearGroupsCommand(11, Date(month, day, year), false), CancellationToken.None);

        Assert.Equal(AcademicYearErrors.CloseDateIsOutOfRange.Code, result.Error.Code);
        _repository.Verify(r => r.GetGroupsToClose(It.IsAny<int>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- the wizard's information and the global list

    [Fact]
    public async Task WizardInfo_HasTheYearsTheCurrentYearAndTheHorizon()
    {
        List<AcademicYearInfoResponse> years =
            [new(11, "2026-2027", Date(9, 1, 2026), Date(9, 1, 2027), 130, 56, 56)];
        _repository.Setup(r => r.GetAcademicYearsInfo(It.IsAny<CancellationToken>())).ReturnsAsync(years);

        Result<AcademicYearWizardInfoResponse> result = await new GetAcademicYearWizardInfoQueryHandler(
                _repository.Object, _lessonGeneratorRepository.Object, _timeProvider.Object)
            .Handle(new GetAcademicYearWizardInfoQuery(), CancellationToken.None);

        Assert.Same(years, result.Value.AcademicYears);
        Assert.Equal((11, Date(11, 1, 2027), Date(11, 30, 2027)),
            (result.Value.CurrentAcademicYearId, result.Value.LastOperationMonth, result.Value.HorizonEnd));
    }

    [Fact]
    public async Task WizardInfo_WithoutMonths_HasNoHorizon()
    {
        _repository.Setup(r => r.GetAcademicYearsInfo(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _lessonGeneratorRepository.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        Result<AcademicYearWizardInfoResponse> result = await new GetAcademicYearWizardInfoQueryHandler(
                _repository.Object, _lessonGeneratorRepository.Object, _timeProvider.Object)
            .Handle(new GetAcademicYearWizardInfoQuery(), CancellationToken.None);

        Assert.Null(result.Value.HorizonEnd);
    }

    [Fact]
    public async Task AcademicYears_AreOrderedByTheStartWithTheCurrentOne()
    {
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Year(12, 2027), Year(10, 2025), Year(11, 2026)]);

        Result<AcademicYearsResponse> result =
            await new GetAcademicYearsQueryHandler(_repository.Object, _timeProvider.Object)
                .Handle(new GetAcademicYearsQuery(), CancellationToken.None);

        Assert.Equal([10, 11, 12], result.Value.AcademicYears.Select(ay => ay.Id));
        Assert.Equal("2025-2026", result.Value.AcademicYears[0].Name);
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
    }

    // --- the next contract number of a year

    [Fact]
    public async Task NextNumber_IsTheNextFreeNumberOfTheYear()
    {
        var studentContracts = new Mock<IStudentContractsRepository>();
        studentContracts.Setup(r => r.GetAcademicYear(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Year(12, 2027));
        studentContracts.Setup(r => r.GetContractNumbers(12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["7.001", "7.002"]);

        Result<StudentContractNextNumberResponse> result =
            await new GetStudentContractNextNumberQueryHandler(studentContracts.Object)
                .Handle(new GetStudentContractNextNumberQuery(12), CancellationToken.None);

        Assert.Equal("7.003", result.Value.ContractNumber);
    }

    [Fact]
    public async Task NextNumber_UnknownYear_IsAnError()
    {
        Result<StudentContractNextNumberResponse> result =
            await new GetStudentContractNextNumberQueryHandler(new Mock<IStudentContractsRepository>().Object)
                .Handle(new GetStudentContractNextNumberQuery(99), CancellationToken.None);

        Assert.Equal(StudentContractErrors.AcademicYearNotFound.Code, result.Error.Code);
    }
}
