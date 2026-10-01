using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Payments.CreatePayment;
using AppMimosiGe.Application.Payments.UpdatePayment;
using AppMimosiGe.Application.Payments.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.Payments;

public sealed class PaymentValidatorsTests
{
    private static readonly DateTime PayDate = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IPaymentsRepository> _repository = new();

    public PaymentValidatorsTests()
    {
        _repository.Setup(r => r.StudentContractExists(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.BankAccountExists(It.IsInRange(1, 9, Moq.Range.Inclusive),
            It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static PaymentRequest Request(int studentContractId = 10, DateTime? payDate = null, decimal amount = 150m,
        string? document = null, int? bankAccountId = 1)
    {
        return new PaymentRequest
        {
            StudentContractId = studentContractId,
            PayDate = payDate ?? PayDate,
            Amount = amount,
            Document = document,
            BankAccountId = bankAccountId
        };
    }

    private async Task<string[]> CreateErrorCodes(PaymentRequest? request)
    {
        ValidationResult result =
            await new CreatePaymentCommandValidator(_repository.Object).ValidateAsync(
                new CreatePaymentCommand(request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    private async Task<string[]> UpdateErrorCodes(PaymentRequest? request)
    {
        ValidationResult result =
            await new UpdatePaymentCommandValidator(_repository.Object).ValidateAsync(
                new UpdatePaymentCommand(5, request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    [Fact]
    public async Task ValidRequest_HasNoErrors()
    {
        Assert.Empty(await CreateErrorCodes(Request(document: new string('ა', 255))));
        Assert.Empty(await UpdateErrorCodes(Request(bankAccountId: 9)));
    }

    // "transfer" between contracts and last year's debt are negative payments
    [Fact]
    public async Task NegativeAmount_IsValid()
    {
        Assert.Empty(await CreateErrorCodes(Request(amount: -0.01m)));
    }

    [Fact]
    public async Task NullRequest_CouldNotBeDecrypted()
    {
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code], await CreateErrorCodes(null));
        Assert.Equal([CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code], await UpdateErrorCodes(null));
    }

    [Fact]
    public async Task MissingContract_IsNotFound()
    {
        Assert.Equal([PaymentErrors.StudentContractNotFound.Code], await CreateErrorCodes(Request(11)));
        Assert.Equal([PaymentErrors.StudentContractNotFound.Code], await UpdateErrorCodes(Request(11)));
    }

    [Fact]
    public async Task EmptyPayDate_IsRequired()
    {
        Assert.Equal([PaymentErrors.PayDateIsRequired.Code], await CreateErrorCodes(Request(payDate: DateTime.MinValue)));
    }

    [Fact]
    public async Task ZeroAmount_IsInvalid()
    {
        Assert.Equal([PaymentErrors.AmountMustNotBeZero.Code], await UpdateErrorCodes(Request(amount: 0m)));
    }

    [Theory]
    [InlineData(10.005)]
    [InlineData(-0.001)]
    public async Task AmountWithMoreThanTwoDecimals_IsInvalid(double amount)
    {
        Assert.Equal([PaymentErrors.AmountHasTooManyDecimals.Code],
            await CreateErrorCodes(Request(amount: (decimal)amount)));
    }

    // trailing zeros are no extra decimals
    [Fact]
    public async Task AmountWithTrailingZeros_IsValid()
    {
        Assert.Empty(await CreateErrorCodes(Request(amount: 10.5000m)));
    }

    [Fact]
    public async Task DocumentLongerThan255_IsTooLong()
    {
        Assert.Equal([PaymentErrors.DocumentIsTooLong.Code],
            await CreateErrorCodes(Request(document: new string('ა', 256))));
    }

    // the length is checked as the document is saved: trimmed
    [Fact]
    public async Task Document255WithSpaces_IsValid()
    {
        Assert.Empty(await CreateErrorCodes(Request(document: $"  {new string('ა', 255)}  ")));
    }

    [Fact]
    public async Task MissingBankAccount_IsRequired()
    {
        Assert.Equal([PaymentErrors.BankAccountIsRequired.Code], await CreateErrorCodes(Request(bankAccountId: null)));
    }

    [Fact]
    public async Task UnknownBankAccount_IsNotFound()
    {
        Assert.Equal([PaymentErrors.BankAccountNotFound.Code], await UpdateErrorCodes(Request(bankAccountId: 99)));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0.01, true)]
    [InlineData(-120.5, true)]
    [InlineData(0.001, false)]
    [InlineData(99.999, false)]
    public void HasAllowedDecimals_AllowsAtMostTwo(double amount, bool expected)
    {
        Assert.Equal(expected, PaymentRequestValidator.HasAllowedDecimals((decimal)amount));
    }
}
