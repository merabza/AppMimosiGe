using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Balances.Models;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class NextPayDatesRecountTests
{
    private static readonly DateTime Today = At(10, 1);

    private readonly Mock<IBalancesRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    private static StudentContract Contract(int scId, DateTime? nextPayDate) =>
        new() { ScId = scId, ContractNumber = $"6.00{scId}", NextPayDate = nextPayDate, DirtyNextPayDate = true };

    [Fact]
    public async Task Run_NoContracts_SavesNothing()
    {
        // Arrange
        _repository.Setup(r => r.GetStudentContractsForRecount(true, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        NextPayDatesRecountResult result = await NextPayDatesRecount.Run(_repository.Object, _unitOfWork.Object, true,
            Today, CancellationToken.None);

        // Assert
        Assert.Equal(new NextPayDatesRecountResult(0, 0), result);
        _repository.Verify(r => r.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Run_SetsEachContractsNextPayDateAndClearsTheFlag(bool onlyDirty)
    {
        // Arrange: 1 owes since 03.09 (was stored), 2 is covered (had a date), 3 has no operation, 4 owes (was empty)
        StudentContract[] contracts =
            [Contract(1, At(9, 3, 15)), Contract(2, At(9, 10, 15)), Contract(3, null), Contract(4, null)];
        _repository.Setup(r => r.GetStudentContractsForRecount(onlyDirty, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. contracts]);
        IReadOnlyCollection<int>? chargesFor = null;
        IReadOnlyCollection<int>? paymentsFor = null;
        _repository.Setup(r => r.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<int>?, CancellationToken>((ids, _) => chargesFor = ids).ReturnsAsync([
                new ChargeData(31, 1, At(9, 3, 15), "English", 48m, 8f, 1f),
                new ChargeData(32, 2, At(9, 3, 15), "English", 48m, 8f, 1f),
                new ChargeData(33, 4, At(9, 17, 15), "English", 48m, 8f, 1f)
            ]);
        _repository.Setup(r => r.GetPayments(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<int>?, CancellationToken>((ids, _) => paymentsFor = ids)
            .ReturnsAsync([new PaymentData(7, 2, At(9, 1), null, 6m)]);

        // Act
        NextPayDatesRecountResult result = await NextPayDatesRecount.Run(_repository.Object, _unitOfWork.Object,
            onlyDirty, Today, CancellationToken.None);

        // Assert: 2 and 4 changed
        Assert.Equal(new NextPayDatesRecountResult(4, 2), result);
        Assert.Equal([1, 2, 3, 4], chargesFor);
        Assert.Equal([1, 2, 3, 4], paymentsFor);
        Assert.Equal([At(9, 3, 15), null, null, At(9, 17, 15)], contracts.Select(c => c.NextPayDate));
        Assert.All(contracts, c => Assert.False(c.DirtyNextPayDate));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    //without a payment the search runs until today: a contract that only has future lessons still gets the first one
    [Fact]
    public async Task Run_UsesTodayAsTheLastPayDateWithoutPayments()
    {
        // Arrange
        StudentContract contract = Contract(1, null);
        _repository.Setup(r => r.GetStudentContractsForRecount(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([contract]);
        _repository.Setup(r => r.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChargeData(31, 1, At(10, 5, 15), "English", 48m, 8f, 1f),
                new ChargeData(32, 1, At(10, 12, 15), "English", 48m, 8f, 1f)
            ]);
        _repository.Setup(r => r.GetPayments(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await NextPayDatesRecount.Run(_repository.Object, _unitOfWork.Object, true, Today, CancellationToken.None);

        // Assert
        Assert.Equal(At(10, 5, 15), contract.NextPayDate);
    }
}
