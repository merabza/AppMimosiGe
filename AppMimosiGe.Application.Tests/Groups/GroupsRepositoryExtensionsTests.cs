using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups;
using MimosiGeCore.Domain.Models;
using Moq;
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
}
