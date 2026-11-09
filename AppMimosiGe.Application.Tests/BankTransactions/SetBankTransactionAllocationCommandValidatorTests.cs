using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;
using AppMimosiGe.Application.Payments;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class SetBankTransactionAllocationCommandValidatorTests
{
    private readonly Mock<IPaymentsRepository> _repository = new();

    public SetBankTransactionAllocationCommandValidatorTests()
    {
        _repository.Setup(r => r.StudentContractExists(It.IsInRange(10, 19, Moq.Range.Inclusive),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private async Task<string[]> ErrorCodes(params (int ScId, decimal Amount)[] shares)
    {
        ValidationResult result = await new SetBankTransactionAllocationCommandValidator(_repository.Object)
            .ValidateAsync(new SetBankTransactionAllocationCommand(7, new BankTransactionAllocationRequest
            {
                Shares = [.. shares.Select(s => new BankTransactionShareRequest
                {
                    StudentContractId = s.ScId, Amount = s.Amount
                })]
            }));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Fact]
    public async Task ValidShares_AndNoShares_HaveNoErrors()
    {
        Assert.Empty(await ErrorCodes((10, 0.01m), (11, 150m)));
        Assert.Empty(await ErrorCodes());
    }

    [Fact]
    public async Task NullRequest_CouldNotBeDecrypted()
    {
        ValidationResult result = await new SetBankTransactionAllocationCommandValidator(_repository.Object)
            .ValidateAsync(new SetBankTransactionAllocationCommand(7, null));

        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code],
            result.Errors.Select(e => e.ErrorCode));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task NonPositiveAmount_IsInvalid(int amount)
    {
        Assert.Equal([BankTransactionErrors.ShareAmountMustBePositive.Code], await ErrorCodes((10, amount)));
    }

    [Fact]
    public async Task AmountWithMoreThanTwoDecimals_IsInvalid()
    {
        Assert.Equal([BankTransactionErrors.ShareAmountHasTooManyDecimals.Code], await ErrorCodes((10, 1.005m)));
    }

    [Fact]
    public async Task MissingContract_IsNotFound()
    {
        Assert.Equal([BankTransactionErrors.ShareStudentContractNotFound.Code], await ErrorCodes((20, 5m)));
    }

    [Fact]
    public async Task RepeatedContract_IsInvalid()
    {
        Assert.Equal([BankTransactionErrors.ShareStudentContractIsRepeated.Code],
            await ErrorCodes((10, 5m), (10, 6m)));
    }
}
