using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.Groups.GroupTestData;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupsRepositoryExtensionsTests
{
    private readonly Mock<IGroupsRepository> _repository = new();

    [Fact]
    public async Task GetDefaultSalarySchemes_RowsWithoutScheme_ReturnTheirContractsDefaultSchemes()
    {
        // Arrange
        _repository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>())).ReturnsAsync(9);
        _repository.Setup(r => r.GetDefaultSalarySchemeId(6, It.IsAny<CancellationToken>())).ReturnsAsync(14);

        // Act
        Dictionary<int, int> result = await _repository.Object.GetDefaultSalarySchemes([
            Teacher(teacherContractId: 5, salarySchemaId: null),
            Teacher(teacherContractId: 6, salarySchemaId: null),
            Teacher(teacherContractId: 7, salarySchemaId: 3)
        ], CancellationToken.None);

        // Assert
        Assert.Equal(new Dictionary<int, int> { [5] = 9, [6] = 14 }, result);
        _repository.Verify(r => r.GetDefaultSalarySchemeId(7, It.IsAny<CancellationToken>()), Times.Never);
    }

    // a contract in several rows is looked up once
    [Fact]
    public async Task GetDefaultSalarySchemes_SameContractInSeveralRows_IsLookedUpOnce()
    {
        // Arrange
        _repository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>())).ReturnsAsync(9);

        // Act
        Dictionary<int, int> result = await _repository.Object.GetDefaultSalarySchemes(
            [Teacher(salarySchemaId: null), Teacher(salarySchemaId: null)], CancellationToken.None);

        // Assert
        Assert.Equal(9, Assert.Single(result).Value);
        _repository.Verify(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDefaultSalarySchemes_EveryRowHasAScheme_QueriesNothing()
    {
        // Act
        Dictionary<int, int> result =
            await _repository.Object.GetDefaultSalarySchemes([Teacher(salarySchemaId: 8)], CancellationToken.None);

        // Assert
        Assert.Empty(result);
        _repository.Verify(r => r.GetDefaultSalarySchemeId(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // the validator rejects such a row first, so this only happens when the contract changed meanwhile
    [Fact]
    public async Task GetDefaultSalarySchemes_ContractWithoutDefaultScheme_Throws()
    {
        // Arrange
        _repository.Setup(r => r.GetDefaultSalarySchemeId(5, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _repository.Object.GetDefaultSalarySchemes([Teacher(salarySchemaId: null)], CancellationToken.None));

        // Assert
        Assert.Equal("Teacher contract 5 has no default salary scheme", ex.Message);
    }

    [Fact]
    public async Task MarkNextPayDatesDirty_LoadsEveryContractOnceAndSetsItsFlag()
    {
        // Arrange
        IReadOnlyCollection<int>? requested = null;
        List<StudentContract> contracts =
        [
            new() { ScId = 20, ContractNumber = "6.001", DirtyNextPayDate = false },
            new() { ScId = 21, ContractNumber = "6.002", DirtyNextPayDate = false }
        ];
        _repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<int>, CancellationToken>((ids, _) => requested = ids).ReturnsAsync(contracts);

        // Act
        await _repository.Object.MarkNextPayDatesDirty([20, 21, 20], CancellationToken.None);

        // Assert
        Assert.NotNull(requested);
        Assert.Equal([20, 21], requested.Order());
        Assert.All(contracts, c => Assert.True(c.DirtyNextPayDate));
    }

    [Fact]
    public async Task MarkNextPayDatesDirty_NoContracts_QueriesNothing()
    {
        // Act
        await _repository.Object.MarkNextPayDatesDirty([], CancellationToken.None);

        // Assert
        _repository.Verify(
            r => r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //the last day of the overlap with group 7 (15.09–01.10), so it still counts (D203)
    private static readonly DateTime Today = Day(9, 30);

    //group 42: contract 5 until 01.10 and again since 01.11, contract 6 in between; Mondays 15:00–16:30; the other side
    //(D201) all the time
    private static GroupSchedule Schedule() =>
        new(42, "1001", null, [
            new GroupScheduleMember(5, StartDate, Day(10, 1)), new GroupScheduleMember(6, Day(10, 1), Day(11, 1)),
            new GroupScheduleMember(5, Day(11, 1), null)
        ], [new GroupScheduleDayTime(1, new TimeOnly(15, 0), 1.5f, StartDate, null)],
        [new GroupSchedulePeriod(StartDate, null)]);

    //group 7 ("1002"), where contract 5 teaches or learns on Mondays at the given time until 01.10, with the other side
    private static List<GroupSchedule> OtherSchedules(TimeOnly mondayStartTime) =>
    [
        new(7, "1002", null, [new GroupScheduleMember(5, Day(9, 1), Day(10, 1))],
            [new GroupScheduleDayTime(1, mondayStartTime, 1.5f, Day(9, 1), null)],
            [new GroupSchedulePeriod(Day(9, 1), null)])
    ];

    private void WithOtherGroup(TimeOnly mondayStartTime)
    {
        _repository.Setup(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(OtherSchedules(mondayStartTime));
    }

    private void WithOtherGroupOfStudent(TimeOnly mondayStartTime)
    {
        _repository.Setup(r => r.GetStudentSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(OtherSchedules(mondayStartTime));
    }

    private void WithWeekDays()
    {
        _repository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>())).ReturnsAsync([
            new LookupItemResponse(1, "ორშაბათი"), new LookupItemResponse(3, "ოთხშაბათი")
        ]);
    }

    //D178: the other groups of the teachers (each contract once, the group itself excepted)
    [Fact]
    public async Task FindTeacherTimesOverlap_NoOverlap_IsNullWithoutLoadingNames()
    {
        // Arrange
        WithOtherGroup(new TimeOnly(16, 30));

        // Act
        Error? error = await _repository.Object.FindTeacherTimesOverlap(Schedule(), Today, CancellationToken.None);

        // Assert
        Assert.Null(error);
        _repository.Verify(
            r => r.GetTeacherSchedules(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(5) && ids.Contains(6)), 42,
                It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetWeekDays(It.IsAny<CancellationToken>()), Times.Never);
    }

    //D203: on 01.10 the overlap with group 7 (15.09–01.10) is over
    [Fact]
    public async Task FindTeacherTimesOverlap_OverlapThatIsOver_IsNullWithoutLoadingNames()
    {
        // Arrange
        WithOtherGroup(new TimeOnly(16, 0));

        // Act
        Error? error =
            await _repository.Object.FindTeacherTimesOverlap(Schedule(), Day(10, 1), CancellationToken.None);

        // Assert
        Assert.Null(error);
        _repository.Verify(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetWeekDays(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FindTeacherTimesOverlap_Overlap_IsConflictWithTheNamesOfTheTeacherAndTheWeekDay()
    {
        // Arrange
        WithOtherGroup(new TimeOnly(16, 0));
        _repository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync([
            new GroupTeacherContractLookupResponse(6, "Beta Bob / T6", null),
            new GroupTeacherContractLookupResponse(5, "Alpha Ann / T5", 8)
        ]);
        WithWeekDays();

        // Act
        Error? error = await _repository.Object.FindTeacherTimesOverlap(Schedule(), Today, CancellationToken.None);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(nameof(GroupErrors.TeacherTimesOverlap), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Contains(": Alpha Ann / T5 — ჯგუფი 1002, ორშაბათი 16:00–17:30, 15.09.2026–01.10.2026. ",
            error.Description, StringComparison.Ordinal);
    }

    //D179: the other groups of the students (each contract once, the group itself excepted)
    [Fact]
    public async Task FindStudentTimesOverlap_NoOverlap_IsNullWithoutLoadingNames()
    {
        // Arrange
        WithOtherGroupOfStudent(new TimeOnly(16, 30));

        // Act
        Error? error = await _repository.Object.FindStudentTimesOverlap(Schedule(), Today, CancellationToken.None);

        // Assert
        Assert.Null(error);
        _repository.Verify(
            r => r.GetStudentSchedules(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(5) && ids.Contains(6)), 42,
                It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(
            r => r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(r => r.GetWeekDays(It.IsAny<CancellationToken>()), Times.Never);
    }

    //D203: on 01.10 the overlap with group 7 (15.09–01.10) is over
    [Fact]
    public async Task FindStudentTimesOverlap_OverlapThatIsOver_IsNullWithoutLoadingNames()
    {
        // Arrange
        WithOtherGroupOfStudent(new TimeOnly(16, 0));

        // Act
        Error? error =
            await _repository.Object.FindStudentTimesOverlap(Schedule(), Day(10, 1), CancellationToken.None);

        // Assert
        Assert.Null(error);
        _repository.Verify(
            r => r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(r => r.GetWeekDays(It.IsAny<CancellationToken>()), Times.Never);
    }

    //only the names of the overlapping students are loaded
    [Fact]
    public async Task FindStudentTimesOverlap_Overlap_IsConflictWithTheNamesOfTheStudentAndTheWeekDay()
    {
        // Arrange
        WithOtherGroupOfStudent(new TimeOnly(16, 0));
        _repository.Setup(r => r.GetStudentContractNames(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1 && ids.Contains(5)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [5] = "Gamma Gia / 6.005" });
        WithWeekDays();

        // Act
        Error? error = await _repository.Object.FindStudentTimesOverlap(Schedule(), Today, CancellationToken.None);

        // Assert
        Assert.NotNull(error);
        Assert.Equal(nameof(GroupErrors.StudentTimesOverlap), error.Code);
        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Contains(": Gamma Gia / 6.005 — ჯგუფი 1002, ორშაბათი 16:00–17:30, 15.09.2026–01.10.2026. ",
            error.Description, StringComparison.Ordinal);
        _repository.Verify(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()), Times.Never);
    }
}
