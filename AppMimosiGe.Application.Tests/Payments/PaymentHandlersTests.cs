using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Payments.CreatePayment;
using AppMimosiGe.Application.Payments.DeletePayment;
using AppMimosiGe.Application.Payments.GetPayment;
using AppMimosiGe.Application.Payments.GetPaymentFormLookups;
using AppMimosiGe.Application.Payments.GetPaymentsRowsData;
using AppMimosiGe.Application.Payments.GetPaymentStudentContracts;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGe.Application.Payments.UpdatePayment;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Payments;

public sealed class PaymentHandlersTests
{
    private static readonly DateTime PayDate = new(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IUserClaimRights> _claimRights = new();

    //the contracts the handler loaded to mark dirty, and the ids it asked for
    private readonly List<StudentContract> _loadedContracts = [];
    private readonly Mock<IPaymentsRepository> _repository = new();
    private readonly List<int> _requestedContractIds = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public PaymentHandlersTests()
    {
        _repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            {
                _requestedContractIds.AddRange(ids);
                _loadedContracts.AddRange(ids.Select(Contract));
                return [.. _loadedContracts];
            });
    }

    private static StudentContract Contract(int scId)
    {
        return new StudentContract { ScId = scId, ContractNumber = $"6.0{scId}", DirtyNextPayDate = false };
    }

    private static PaymentRequest Request(int studentContractId = 10, bool isChecked = false)
    {
        return new PaymentRequest
        {
            StudentContractId = studentContractId,
            PayDate = PayDate.AddHours(15).AddMinutes(20),
            Amount = -120.5m,
            Document = "  bank 7  ",
            BankAccountId = 9,
            Checked = isChecked
        };
    }

    private Payment WithExistingPayment(bool isChecked)
    {
        var payment = new Payment
        {
            Id = 5,
            StudentContractId = 10,
            PayDate = PayDate,
            Amount = 300m,
            Document = "old",
            BankAccountId = 1,
            Checked = isChecked
        };
        _repository.Setup(r => r.GetForChange(5, It.IsAny<CancellationToken>())).ReturnsAsync(payment);
        return payment;
    }

    private void WithCheckRight(bool hasRight)
    {
        _claimRights.Setup(c => c.HasClaim(PaymentClaims.CheckPayments, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRight);
    }

    private Task<Result<int>> Create(PaymentRequest request)
    {
        return new CreatePaymentCommandHandler(_repository.Object, _claimRights.Object, _unitOfWork.Object).Handle(
            new CreatePaymentCommand(request), CancellationToken.None);
    }

    private Task<Result> Update(PaymentRequest request)
    {
        return new UpdatePaymentCommandHandler(_repository.Object, _claimRights.Object, _unitOfWork.Object).Handle(
            new UpdatePaymentCommand(5, request), CancellationToken.None);
    }

    private Task<Result> Delete()
    {
        return new DeletePaymentCommandHandler(_repository.Object, _claimRights.Object, _unitOfWork.Object).Handle(
            new DeletePaymentCommand(5), CancellationToken.None);
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    private void VerifyRightAsked(Times times)
    {
        _claimRights.Verify(c => c.HasClaim(It.IsAny<string>(), It.IsAny<CancellationToken>()), times);
    }

    private static void AssertError(Result result, Error error)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(error.Code, result.Error.Code);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task Create_AddsThePaymentWithTheRequestFields()
    {
        // Arrange
        Payment? added = null;
        _repository.Setup(r => r.Add(It.IsAny<Payment>())).Callback<Payment>(p =>
        {
            added = p;
            p.Id = 77;
        });

        // Act
        Result<int> result = await Create(Request());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(77, result.Value);
        Assert.NotNull(added);
        Assert.Equal(10, added.StudentContractId);
        Assert.Equal(PayDate, added.PayDate);
        Assert.Equal(-120.5m, added.Amount);
        Assert.Equal("bank 7", added.Document);
        Assert.Equal(9, added.BankAccountId);
        Assert.False(added.Checked);
        VerifySaved(Times.Once());
    }

    // an unchecked payment needs no special right, so the right is not even looked up
    [Fact]
    public async Task Create_Unchecked_DoesNotAskForTheRight()
    {
        // Act
        await Create(Request());

        // Assert
        VerifyRightAsked(Times.Never());
    }

    // Access set DirtyNextPayDate on every contract; only the payment's contract is marked here (D78)
    [Fact]
    public async Task Create_MarksThePaymentsContractDirty()
    {
        // Act
        await Create(Request(12));

        // Assert
        Assert.Equal([12], _requestedContractIds);
        Assert.True(Assert.Single(_loadedContracts).DirtyNextPayDate);
    }

    [Fact]
    public async Task Create_CheckedWithTheRight_IsSavedChecked()
    {
        // Arrange
        WithCheckRight(true);
        Payment? added = null;
        _repository.Setup(r => r.Add(It.IsAny<Payment>())).Callback<Payment>(p => added = p);

        // Act
        Result<int> result = await Create(Request(isChecked: true));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(added?.Checked);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Create_CheckedWithoutTheRight_IsRefusedAndNothingIsAdded()
    {
        // Arrange
        WithCheckRight(false);

        // Act
        Result<int> result = await Create(Request(isChecked: true));

        // Assert
        AssertError(result, PaymentErrors.CheckedRequiresRight);
        _repository.Verify(r => r.Add(It.IsAny<Payment>()), Times.Never);
        Assert.Empty(_loadedContracts);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_AppliesTheRequestFields()
    {
        // Arrange
        Payment payment = WithExistingPayment(false);

        // Act
        Result result = await Update(Request(11));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(11, payment.StudentContractId);
        Assert.Equal(PayDate, payment.PayDate);
        Assert.Equal(-120.5m, payment.Amount);
        Assert.Equal("bank 7", payment.Document);
        Assert.Equal(9, payment.BankAccountId);
        Assert.False(payment.Checked);
        VerifyRightAsked(Times.Never());
        VerifySaved(Times.Once());
    }

    // the balance of both contracts changes when a payment moves to another contract (D78)
    [Fact]
    public async Task Update_MarksTheOldAndTheNewContractDirty()
    {
        // Arrange
        WithExistingPayment(false);

        // Act
        await Update(Request(11));

        // Assert
        Assert.Equal([10, 11], _requestedContractIds.Order());
        Assert.All(_loadedContracts, c => Assert.True(c.DirtyNextPayDate));
    }

    [Fact]
    public async Task Update_SameContract_MarksItOnce()
    {
        // Arrange
        WithExistingPayment(false);

        // Act
        await Update(Request());

        // Assert
        Assert.Equal([10], _requestedContractIds);
    }

    [Fact]
    public async Task Update_MissingPayment_IsNotFoundAndSavesNothing()
    {
        // Act
        Result result = await Update(Request());

        // Assert
        AssertError(result, PaymentErrors.PaymentNotFound);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_CheckedPaymentWithoutTheRight_IsRefusedAndUnchanged()
    {
        // Arrange
        WithCheckRight(false);
        Payment payment = WithExistingPayment(true);

        // Act
        Result result = await Update(Request(11, true));

        // Assert
        AssertError(result, PaymentErrors.PaymentIsChecked);
        Assert.Equal(10, payment.StudentContractId);
        Assert.Equal(300m, payment.Amount);
        Assert.True(payment.Checked);
        Assert.Empty(_loadedContracts);
        VerifySaved(Times.Never());
    }

    // unchecking is a change of a checked payment as well
    [Fact]
    public async Task Update_UncheckWithoutTheRight_IsRefused()
    {
        // Arrange
        WithCheckRight(false);
        Payment payment = WithExistingPayment(true);

        // Act
        Result result = await Update(Request());

        // Assert
        AssertError(result, PaymentErrors.PaymentIsChecked);
        Assert.True(payment.Checked);
    }

    [Fact]
    public async Task Update_CheckWithoutTheRight_IsRefused()
    {
        // Arrange
        WithCheckRight(false);
        Payment payment = WithExistingPayment(false);

        // Act
        Result result = await Update(Request(isChecked: true));

        // Assert
        AssertError(result, PaymentErrors.CheckedRequiresRight);
        Assert.False(payment.Checked);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_CheckWithTheRight_IsSavedChecked()
    {
        // Arrange
        WithCheckRight(true);
        Payment payment = WithExistingPayment(false);

        // Act
        Result result = await Update(Request(isChecked: true));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(payment.Checked);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Update_CheckedPaymentWithTheRight_CanBeChangedAndUnchecked()
    {
        // Arrange
        WithCheckRight(true);
        Payment payment = WithExistingPayment(true);

        // Act
        Result result = await Update(Request(11));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(11, payment.StudentContractId);
        Assert.False(payment.Checked);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_RemovesThePaymentAndMarksItsContractDirty()
    {
        // Arrange
        Payment payment = WithExistingPayment(false);

        // Act
        Result result = await Delete();

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(payment), Times.Once);
        Assert.Equal([10], _requestedContractIds);
        Assert.True(Assert.Single(_loadedContracts).DirtyNextPayDate);
        VerifyRightAsked(Times.Never());
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_MissingPayment_IsNotFound()
    {
        // Act
        Result result = await Delete();

        // Assert
        AssertError(result, PaymentErrors.PaymentNotFound);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_CheckedPaymentWithoutTheRight_IsRefused()
    {
        // Arrange
        WithCheckRight(false);
        WithExistingPayment(true);

        // Act
        Result result = await Delete();

        // Assert
        AssertError(result, PaymentErrors.PaymentIsChecked);
        _repository.Verify(r => r.Remove(It.IsAny<Payment>()), Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Delete_CheckedPaymentWithTheRight_IsRemoved()
    {
        // Arrange
        WithCheckRight(true);
        Payment payment = WithExistingPayment(true);

        // Act
        Result result = await Delete();

        // Assert
        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.Remove(payment), Times.Once);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Get_ReturnsThePayment()
    {
        // Arrange
        var payment = new PaymentResponse(5, 10, "Alpha Ann 6.001", 11, PayDate, 300m, null, 1, true);
        _repository.Setup(r => r.GetOne(5, It.IsAny<CancellationToken>())).ReturnsAsync(payment);

        // Act
        Result<PaymentResponse> result =
            await new GetPaymentQueryHandler(_repository.Object).Handle(new GetPaymentQuery(5), CancellationToken.None);

        // Assert
        Assert.Same(payment, result.Value);
    }

    [Fact]
    public async Task Get_MissingPayment_IsNotFound()
    {
        // Act
        Result<PaymentResponse> result =
            await new GetPaymentQueryHandler(_repository.Object).Handle(new GetPaymentQuery(5), CancellationToken.None);

        // Assert
        AssertError(result, PaymentErrors.PaymentNotFound);
    }

    // the years of the contract forms (sorted by name) with the current one, and the payment types
    [Fact]
    public async Task GetFormLookups_ReturnsTheYearsTheCurrentYearAndTheBankAccounts()
    {
        // Arrange
        var studentContractsRepository = new Mock<IStudentContractsRepository>();
        studentContractsRepository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 11,
                AcademicYearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            },
            new AcademicYear
            {
                AyId = 10,
                AcademicYearName = "2025-2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }
        ]);
        List<LookupItemResponse> bankAccounts = [new(1, "Bank")];
        _repository.Setup(r => r.GetBankAccounts(It.IsAny<CancellationToken>())).ReturnsAsync(bankAccounts);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);

        // Act
        Result<PaymentFormLookupsResponse> result = await new GetPaymentFormLookupsQueryHandler(_repository.Object,
                studentContractsRepository.Object, timeProvider.Object)
            .Handle(new GetPaymentFormLookupsQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(11, result.Value.CurrentAcademicYearId);
        Assert.Equal([new LookupItemResponse(10, "2025-2026"), new LookupItemResponse(11, "2026-2027")],
            result.Value.AcademicYears);
        Assert.Same(bankAccounts, result.Value.BankAccounts);
    }

    [Fact]
    public async Task GetStudentContracts_ReturnsTheContractsOfTheYear()
    {
        // Arrange
        List<LookupItemResponse> contracts = [new(10, "Alpha Ann 6.001")];
        _repository.Setup(r => r.GetStudentContracts(11, It.IsAny<CancellationToken>())).ReturnsAsync(contracts);

        // Act
        Result<List<LookupItemResponse>> result =
            await new GetPaymentStudentContractsQueryHandler(_repository.Object).Handle(
                new GetPaymentStudentContractsQuery(11), CancellationToken.None);

        // Assert
        Assert.Same(contracts, result.Value);
    }

    [Fact]
    public async Task GetRowsData_PassesTheFilter()
    {
        // Arrange
        var rows = new PaymentsRowsDataResponse(0, 0, 0m, []);
        PaymentsListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<PaymentsListQuery>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentsListQuery, CancellationToken>((q, _) => passed = q).ReturnsAsync(rows);

        // Act
        Result<PaymentsRowsDataResponse> result = await new GetPaymentsRowsDataQueryHandler(_repository.Object).Handle(
            new GetPaymentsRowsDataQuery(Encode(
                """{"offset":10,"rowsCount":5,"filterFields":[{"fieldName":"bankAccountId","value":"4"},{"fieldName":"dateFrom","value":"2026-09-01"}]}""")),
            CancellationToken.None);

        // Assert
        Assert.Same(rows, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(5, passed.RowsCount);
        Assert.Equal(4, passed.BankAccountId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), passed.DateFrom);
    }

    // not base64 (FormatException), not JSON (JsonException), JSON null (no request)
    [Theory]
    [InlineData("not base64!")]
    [InlineData("bm90IGpzb24=")]
    [InlineData("bnVsbA==")]
    public async Task GetRowsData_UnreadableRequest_IsInvalid(string filterSortRequest)
    {
        // Act
        Result<PaymentsRowsDataResponse> result =
            await new GetPaymentsRowsDataQueryHandler(_repository.Object).Handle(
                new GetPaymentsRowsDataQuery(filterSortRequest), CancellationToken.None);

        // Assert
        AssertError(result, PaymentErrors.FilterSortRequestIsInvalid);
    }

    [Fact]
    public async Task GetRowsData_InvalidFilter_IsInvalidAndNotLoaded()
    {
        // Act
        Result<PaymentsRowsDataResponse> result = await new GetPaymentsRowsDataQueryHandler(_repository.Object).Handle(
            new GetPaymentsRowsDataQuery(
                Encode("""{"offset":0,"rowsCount":5,"filterFields":[{"fieldName":"bankAccountId","value":"x"}]}""")),
            CancellationToken.None);

        // Assert
        AssertError(result, PaymentErrors.FilterSortRequestIsInvalid);
        _repository.Verify(r => r.GetRowsData(It.IsAny<PaymentsListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
