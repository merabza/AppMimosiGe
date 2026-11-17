using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
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
using Range = Moq.Range;

namespace AppMimosiGe.Application.Tests.Payments;

public sealed class PaymentValidatorsTests
{
    private static readonly DateTime PayDate = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IBankMovementsClient> _bankClient = new();
    private readonly List<IBankMovementsClient> _bankClients = [];
    private readonly Mock<IPaymentsRepository> _repository = new();

    public PaymentValidatorsTests()
    {
        _bankClients.Add(_bankClient.Object);
        _repository.Setup(r => r.StudentContractExists(10, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.BankAccountExists(It.IsInRange(1, 9, Range.Inclusive), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    //decimal literals: since .NET 11 a double such as 0.01 converts to decimal with all its binary digits
    //(0.0100000000000000002081668171), so a double would have too many decimals whatever the rule allows
    public static TheoryData<decimal> AmountsWithMoreThanTwoDecimals => [10.005m, -0.001m];

    public static TheoryData<decimal, bool> AllowedDecimalsAmounts =>
        new()
        {
            { 1m, true },
            { 0.01m, true },
            { -120.5m, true },
            { 0.001m, false },
            { 99.999m, false }
        };

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
            await new CreatePaymentCommandValidator(_repository.Object, _bankClients).ValidateAsync(
                new CreatePaymentCommand(request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    private async Task<string[]> UpdateErrorCodes(PaymentRequest? request)
    {
        ValidationResult result =
            await new UpdatePaymentCommandValidator(_repository.Object, _bankClients).ValidateAsync(
                new UpdatePaymentCommand(5, request));
        return [.. result.Errors.Select(e => e.ErrorCode)];
    }

    // D167: from the integration's start date the integrated account's payments come only from the bank
    [Fact]
    public async Task IntegratedAccountFromStartDate_IsNotEnteredByHand()
    {
        _bankClient.Setup(c => c.Account).Returns(new IntegratedBankAccount(4, PayDate.AddHours(10)));

        Assert.Equal([BankTransactionErrors.ManualPaymentOnIntegratedAccount.Code],
            await CreateErrorCodes(Request(bankAccountId: 4)));
        Assert.Equal([BankTransactionErrors.ManualPaymentOnIntegratedAccount.Code],
            await UpdateErrorCodes(Request(payDate: PayDate.AddDays(1), bankAccountId: 4)));
        // before the start date (history), on another account, or without an integration: as before
        Assert.Empty(await UpdateErrorCodes(Request(payDate: PayDate.AddDays(-1), bankAccountId: 4)));
        Assert.Empty(await CreateErrorCodes(Request(bankAccountId: 5)));
        _bankClient.Setup(c => c.Account).Returns((IntegratedBankAccount?)null);
        Assert.Empty(await CreateErrorCodes(Request(bankAccountId: 4)));
    }

    // D167 holds for every integrated account, each from its own start date
    [Fact]
    public async Task EveryIntegratedAccount_IsNotEnteredByHandFromItsStartDate()
    {
        _bankClient.Setup(c => c.Account).Returns(new IntegratedBankAccount(4, PayDate.AddDays(5)));
        Mock<IBankMovementsClient> bog = new();
        bog.Setup(c => c.Account).Returns(new IntegratedBankAccount(1, PayDate));
        _bankClients.Add(bog.Object);

        Assert.Equal([BankTransactionErrors.ManualPaymentOnIntegratedAccount.Code],
            await CreateErrorCodes(Request(bankAccountId: 1)));
        Assert.Empty(await CreateErrorCodes(Request(bankAccountId: 4)));
        Assert.Equal([BankTransactionErrors.ManualPaymentOnIntegratedAccount.Code],
            await CreateErrorCodes(Request(payDate: PayDate.AddDays(5), bankAccountId: 4)));
        Assert.Empty(await CreateErrorCodes(Request(payDate: PayDate.AddDays(-1), bankAccountId: 1)));
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
        Assert.Equal([PaymentErrors.PayDateIsRequired.Code],
            await CreateErrorCodes(Request(payDate: DateTime.MinValue)));
    }

    [Fact]
    public async Task ZeroAmount_IsInvalid()
    {
        Assert.Equal([PaymentErrors.AmountMustNotBeZero.Code], await UpdateErrorCodes(Request(amount: 0m)));
    }

    [Theory]
    [MemberData(nameof(AmountsWithMoreThanTwoDecimals))]
    public async Task AmountWithMoreThanTwoDecimals_IsInvalid(decimal amount)
    {
        Assert.Equal([PaymentErrors.AmountHasTooManyDecimals.Code], await CreateErrorCodes(Request(amount: amount)));
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
    [MemberData(nameof(AllowedDecimalsAmounts))]
    public void HasAllowedDecimals_AllowsAtMostTwo(decimal amount, bool expected)
    {
        Assert.Equal(expected, PaymentRequestValidator.HasAllowedDecimals(amount));
    }
}
