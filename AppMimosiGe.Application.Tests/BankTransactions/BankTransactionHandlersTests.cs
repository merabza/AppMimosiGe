using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.BankTransactions.GetBankTransaction;
using AppMimosiGe.Application.BankTransactions.GetBankTransactions;
using AppMimosiGe.Application.BankTransactions.GetBankTransactionStudentContracts;
using AppMimosiGe.Application.BankTransactions.ImportBankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;
using AppMimosiGe.Application.BankTransactions.SetBankTransactionIgnored;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Reports.Finance.UnallocatedBankTransactions;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class BankTransactionHandlersTests
{
    private static readonly DateTime Today = new(2026, 11, 9, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime StartDate = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly Mock<IBalancesRepository> _balances = new();
    private readonly Mock<IBankMovementsClient> _bankClient = new();
    private readonly List<IBankMovementsClient> _bankClients = [];
    private readonly List<int> _dirtyContracts = [];
    private readonly Mock<IPaymentsRepository> _payments = new();
    private readonly Mock<IBankTransactionsRepository> _repository = new();
    private readonly List<BankTransaction> _saved = [];
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public BankTransactionHandlersTests()
    {
        _bankClient.Setup(c => c.Account).Returns(new IntegratedBankAccount(4, StartDate));
        _bankClients.Add(_bankClient.Object);
        _repository.Setup(r => r.GetBankNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, string> { [1] = "საქართველოს ბანკი", [4] = "თიბისი" });
        _timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Today.AddHours(9), TimeSpan.Zero));
        _timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        _repository.Setup(r => r.Add(It.IsAny<BankTransaction>())).Callback<BankTransaction>(_saved.Add);
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 6,
                AcademicYearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }
        ]);
        _repository.Setup(r => r.GetMatchContracts(6, It.IsAny<CancellationToken>())).ReturnsAsync([
            new MatchContractData(11, 1, "ნინო", "ბერიძე", 100, "01001000100", "თამარ", "ბერიძე"),
            new MatchContractData(12, 2, "გიორგი", "ბერიძე", 100, "01001000100", "თამარ", "ბერიძე")
        ]);
        _repository.Setup(r => r.GetStudentByContractNumber(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetLearnedPayers(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetExistingMovementIds(4, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(["old"]);
        _balances.Setup(b => b.GetStudentAccountContracts(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<int, int> { [11] = 1, [12] = 2 });
        _balances.Setup(b => b.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ChargeData(1, 11, Today.AddDays(-1), "course", 30m, 1f, 1f),
                new ChargeData(2, 12, Today.AddDays(-1), "course", 10m, 1f, 1f)
            ]);
        _balances.Setup(b => b.GetPayments(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payments.Setup(p =>
                p.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            {
                _dirtyContracts.AddRange(ids);
                return [];
            });
    }

    private static BankMovement Movement(string id, decimal amount, string purpose, string? taxCode = null,
        string partnerName = "თამარ ბერიძე, 01001000100")
    {
        return new BankMovement(id, Today.AddDays(-1), null, amount, "GEL", $"  {purpose}  ", "info", null, "D1",
            partnerName, "GE00TB0000000000000100", "TBCBGE22", taxCode, null);
    }

    private void BankReturns(params BankMovement[] movements)
    {
        _bankClient.Setup(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<IReadOnlyList<BankMovement>>(movements));
    }

    private Task<Result<ImportBankTransactionsResponse>> Import(DateTime? from = null, DateTime? to = null,
        bool dryRun = false)
    {
        return new ImportBankTransactionsCommandHandler(_bankClients, _repository.Object, _balances.Object,
                _payments.Object, _timeProvider.Object, _unitOfWork.Object)
            .Handle(new ImportBankTransactionsCommand(from, to, dryRun), CancellationToken.None);
    }

    private static ImportBankAccountResponse Only(Result<ImportBankTransactionsResponse> result)
    {
        return Assert.Single(result.Value.Accounts);
    }

    private static ImportBankAccountResponse Tbc(DateTime from, DateTime to, int received, int alreadyImported,
        int imported, int allocated, int unallocated, string? error = null)
    {
        return new ImportBankAccountResponse(4, "თიბისი", from, to, received, alreadyImported, imported, allocated,
            unallocated, error);
    }

    [Fact]
    public async Task Import_SavesNewTransactionsWithTheirPaymentsAtOnce()
    {
        BankReturns(Movement("old", 10m, "ნინო ბერიძე"), Movement("2", 40m, "სწავლა", "01001000100"),
            Movement("1", 25m, "უცნობი", partnerName: "შპს კომპანია"));

        Result<ImportBankTransactionsResponse> result = await Import();

        Assert.Equal(Tbc(StartDate, Today, 3, 1, 2, 1, 1), Only(result));
        Assert.False(result.Value.DryRun);
        // by date, then the bank id
        Assert.Equal(["1", "2"], _saved.Select(t => t.MovementId));
        BankTransaction unallocated = _saved[0];
        Assert.Equal((4, 25m, "GEL", "უცნობი", 0, false), (unallocated.BankAccountId, unallocated.Amount,
            unallocated.Currency, unallocated.Purpose, unallocated.MatchStep, unallocated.AllocatedManually));
        Assert.Empty(unallocated.Payments);
        Assert.Equal(Today.AddHours(9), unallocated.ImportedAt);
        // the siblings' debts are 30 and 10: 40 covers both
        BankTransaction allocated = _saved[1];
        Assert.Equal((int)EPaymentMatchStep.PayerPersonalNumber, allocated.MatchStep);
        Assert.Equal([(11, 30m), (12, 10m)], allocated.Payments.Select(p => (p.StudentContractId, p.Amount)));
        Assert.All(allocated.Payments, p =>
        {
            Assert.Equal((Today.AddDays(-1), "სწავლა", 4, false), (p.PayDate, p.Document, p.BankAccountId, p.Checked));
        });
        Assert.Equal([11, 12], _dirtyContracts);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // the default period starts 3 days before the last saved transaction, never before the start date
    [Theory]
    [InlineData(5, 2)]
    [InlineData(1, 0)]
    public async Task Import_DefaultPeriod_OverlapsTheLastTransaction(int lastDay, int expectedFromDay)
    {
        _repository.Setup(r => r.GetLastTransactionDate(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StartDate.AddDays(lastDay).AddHours(10));
        BankReturns();

        Result<ImportBankTransactionsResponse> result = await Import();

        Assert.Equal(StartDate.AddDays(expectedFromDay), Only(result).PeriodFrom);
        _bankClient.Verify(c => c.GetIncomingPayments(DateOnly.FromDateTime(StartDate.AddDays(expectedFromDay)),
            DateOnly.FromDateTime(Today), It.IsAny<CancellationToken>()));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_GivenPeriod_IsCutAtTheStartDate()
    {
        BankReturns();

        Result<ImportBankTransactionsResponse> result = await Import(StartDate.AddDays(-10), Today.AddDays(-2));

        Assert.Equal((StartDate, Today.AddDays(-2)), (Only(result).PeriodFrom, Only(result).PeriodTo));
    }

    // a period wholly before the start date asks the bank nothing
    [Fact]
    public async Task Import_PeriodBeforeTheStartDate_IsEmpty()
    {
        Result<ImportBankTransactionsResponse> result = await Import(StartDate.AddDays(-10), StartDate.AddDays(-5));

        Assert.Equal(Tbc(StartDate, StartDate.AddDays(-5), 0, 0, 0, 0, 0), Only(result));
        _bankClient.Verify(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_DryRun_SavesNothing()
    {
        BankReturns(Movement("2", 40m, "სწავლა", "01001000100"));

        Result<ImportBankTransactionsResponse> result = await Import(dryRun: true);

        Assert.Equal(Tbc(StartDate, Today, 1, 0, 1, 1, 0), Only(result));
        Assert.True(result.Value.DryRun);
        Assert.Empty(_saved);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Import_Errors()
    {
        Assert.Equal(BankTransactionErrors.PeriodIsInvalid.Code, (await Import(Today, Today.AddDays(-1))).Error.Code);

        _bankClient.Setup(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<BankMovement>>(BankTransactionErrors.BankIsUnreachable));
        // the bank's failure is the account's result, not the import's
        Assert.Equal(Tbc(StartDate, Today, 0, 0, 0, 0, 0, BankTransactionErrors.BankIsUnreachable.Description),
            Only(await Import()));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        _bankClient.Setup(c => c.Account).Returns((IntegratedBankAccount?)null);
        Assert.Equal(BankTransactionErrors.BankIsNotConfigured.Code, (await Import()).Error.Code);
    }

    // a second import waits for nobody: it is refused while the first one runs
    [Fact]
    public async Task Import_WhileAnotherRuns_IsRefused()
    {
        TaskCompletionSource<Result<IReadOnlyList<BankMovement>>> bank = new();
        _bankClient.Setup(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>())).Returns(bank.Task);

        Task<Result<ImportBankTransactionsResponse>> first = Import();
        Result<ImportBankTransactionsResponse> second = await Import();
        bank.SetResult(Result.Success<IReadOnlyList<BankMovement>>([]));

        Assert.Equal(BankTransactionErrors.ImportIsRunning.Code, second.Error.Code);
        Assert.True((await first).IsSuccess);
        // the lock is free again
        Assert.True((await Import()).IsSuccess);
    }

    private Mock<IBankMovementsClient> AddBogClient(Result<IReadOnlyList<BankMovement>> received)
    {
        Mock<IBankMovementsClient> bog = new();
        bog.Setup(c => c.Account).Returns(new IntegratedBankAccount(1, StartDate.AddDays(2)));
        bog.Setup(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(received);
        _repository.Setup(r => r.GetExistingMovementIds(1, It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _bankClients.Add(bog.Object);
        return bog;
    }

    // D173: every integrated account in turn, from its own start date; a failing bank stops no other, and a bank
    // without an integrated account is not asked
    [Fact]
    public async Task Import_EveryIntegratedAccountInTurn_AFailureStopsNoOther()
    {
        Mock<IBankMovementsClient> notIntegrated = new();
        _bankClients.Insert(0, notIntegrated.Object);
        Mock<IBankMovementsClient> bog = AddBogClient(
            Result.Failure<IReadOnlyList<BankMovement>>(BankTransactionErrors.BankRateLimitExceeded(42)));
        BankReturns(Movement("2", 40m, "სწავლა", "01001000100"));

        Result<ImportBankTransactionsResponse> result = await Import();

        Assert.Equal([
            Tbc(StartDate, Today, 1, 0, 1, 1, 0),
            new ImportBankAccountResponse(1, "საქართველოს ბანკი", StartDate.AddDays(2), Today, 0, 0, 0, 0, 0,
                BankTransactionErrors.BankRateLimitExceeded(42).Description)
        ], result.Value.Accounts);
        bog.Verify(c => c.GetIncomingPayments(DateOnly.FromDateTime(StartDate.AddDays(2)),
            DateOnly.FromDateTime(Today), It.IsAny<CancellationToken>()));
        notIntegrated.Verify(c => c.GetIncomingPayments(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // each account is saved apart with its own account, and the next account's balances are read after that save
    [Fact]
    public async Task Import_SavesEachAccountApart()
    {
        List<string> calls = [];
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("save"))
            .Returns(Task.CompletedTask);
        _balances.Setup(b => b.GetPayments(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("payments")).ReturnsAsync([]);
        AddBogClient(Result.Success<IReadOnlyList<BankMovement>>([Movement("b1", 10m, "ნინო ბერიძე")]));
        BankReturns(Movement("t1", 10m, "გიორგი ბერიძე"));

        Result<ImportBankTransactionsResponse> result = await Import();

        Assert.Equal([4, 1], result.Value.Accounts.Select(a => a.BankAccountId));
        Assert.Equal([("t1", 4, 12, 4), ("b1", 1, 11, 1)], _saved.Select(t => (t.MovementId, t.BankAccountId,
            Assert.Single(t.Payments).StudentContractId, t.Payments.Single().BankAccountId)));
        Assert.Equal(["payments", "save", "payments", "save"], calls);
        _repository.Verify(r => r.GetLastTransactionDate(1, It.IsAny<CancellationToken>()));
    }

    // no current academic year: nobody is found, every transaction stays unallocated
    [Fact]
    public async Task Import_WithoutAcademicYears_LeavesEverythingUnallocated()
    {
        _repository.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        BankReturns(Movement("2", 40m, "ნინო ბერიძე", "01001000100"));

        Result<ImportBankTransactionsResponse> result = await Import();

        Assert.Equal((1, 0, 1), (Only(result).Imported, Only(result).Allocated, Only(result).Unallocated));
        _balances.Verify(b => b.GetCharges(It.IsAny<IReadOnlyCollection<int>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // texts longer than their columns are cut, empty ones are null
    [Fact]
    public async Task Import_CutsLongTexts()
    {
        BankReturns(new BankMovement("1", Today, null, 10m, "GEL", new string('ა', 600), " ", null, null,
            new string('ბ', 300), null, null, null, null));

        await Import();

        BankTransaction saved = Assert.Single(_saved);
        Assert.Equal((500, null, 255), (saved.Purpose!.Length, saved.AdditionalInformation, saved.PartnerName!.Length));
    }

    private BankTransaction Stored(params Payment[] payments)
    {
        BankTransaction bankTransaction = new()
        {
            Id = 7,
            BankAccountId = 4,
            MovementId = "1",
            Currency = "GEL",
            TransactionDate = Today.AddHours(10),
            Amount = 100m,
            Purpose = "სწავლა",
            PartnerTaxCode = "01001000100",
            PartnerName = "თამარ ბერიძე"
        };
        foreach (Payment payment in payments)
        {
            bankTransaction.Payments.Add(payment);
        }

        _repository.Setup(r => r.GetForChange(7, It.IsAny<CancellationToken>())).ReturnsAsync(bankTransaction);
        return bankTransaction;
    }

    private Task<Result> Allocate(params (int ScId, decimal Amount)[] shares)
    {
        return new SetBankTransactionAllocationCommandHandler(_repository.Object, _payments.Object, _unitOfWork.Object)
            .Handle(new SetBankTransactionAllocationCommand(7, new BankTransactionAllocationRequest
            {
                Shares = [.. shares.Select(s => new BankTransactionShareRequest
                {
                    StudentContractId = s.ScId, Amount = s.Amount
                })]
            }), CancellationToken.None);
    }

    // a share of the same contract changes the payment's amount, a missing one is deleted, a new one is added; the
    // payer goes to the new shares' students that do not have him yet
    [Fact]
    public async Task Allocation_MatchesThePaymentsToTheShares()
    {
        Payment kept = new() { Id = 1, StudentContractId = 11, Amount = 50m };
        Payment removed = new() { Id = 2, StudentContractId = 12, Amount = 50m };
        BankTransaction bankTransaction = Stored(kept, removed);
        _repository.Setup(r => r.GetStudentHumanIds(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<int, int> { [11] = 1, [13] = 3 });
        _repository.Setup(r => r.GetStudentPayers(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StudentPayer { StudentHumanId = 1, PersonalNumber = "01001000100" }]);
        List<StudentPayer> learned = [];
        _repository.Setup(r => r.AddStudentPayer(It.IsAny<StudentPayer>())).Callback<StudentPayer>(learned.Add);

        Result result = await Allocate((11, 60m), (13, 40m));

        Assert.True(result.IsSuccess);
        Assert.Equal([(11, 60m), (13, 40m)], bankTransaction.Payments.Select(p => (p.StudentContractId, p.Amount)));
        Assert.Same(kept, bankTransaction.Payments.First());
        Payment added = bankTransaction.Payments.Last();
        Assert.Equal((Today, "სწავლა", 4, false), (added.PayDate, added.Document, added.BankAccountId, added.Checked));
        _repository.Verify(r => r.RemovePayment(removed));
        Assert.True(bankTransaction.AllocatedManually);
        Assert.Equal([3], learned.Select(l => l.StudentHumanId));
        Assert.Equal([11, 12, 13], _dirtyContracts);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // the whole amount may be allocated, an empty list removes the allocation and learns nothing
    [Fact]
    public async Task Allocation_EmptyRemovesEverything()
    {
        BankTransaction bankTransaction = Stored(new Payment { Id = 1, StudentContractId = 11, Amount = 100m });

        Assert.True((await Allocate()).IsSuccess);

        Assert.Empty(bankTransaction.Payments);
        _repository.Verify(r => r.GetStudentHumanIds(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Allocation_LongPurpose_IsCutForTheDocument()
    {
        BankTransaction bankTransaction = Stored();
        bankTransaction.Purpose = new string('ა', 300);
        _repository.Setup(r => r.GetStudentHumanIds(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repository.Setup(r => r.GetStudentPayers(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);

        Assert.True((await Allocate((11, 100m))).IsSuccess);

        Assert.Equal(255, Assert.Single(bankTransaction.Payments).Document!.Length);
    }

    [Fact]
    public async Task Allocation_Errors()
    {
        Assert.Equal(BankTransactionErrors.BankTransactionNotFound.Code, (await Allocate()).Error.Code);

        BankTransaction bankTransaction = Stored();
        Assert.Equal(BankTransactionErrors.AllocationExceedsAmount.Code,
            (await Allocate((11, 60m), (12, 40.01m))).Error.Code);

        bankTransaction.Ignored = true;
        Assert.Equal(BankTransactionErrors.BankTransactionIsIgnored.Code, (await Allocate((11, 1m))).Error.Code);

        Stored(new Payment { StudentContractId = 11, Amount = 1m, Checked = true });
        Assert.Equal(BankTransactionErrors.BankTransactionHasCheckedPayments.Code, (await Allocate()).Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // an ignored transaction may lose its (empty) allocation
    [Fact]
    public async Task Allocation_EmptyOnAnIgnoredTransaction_IsAllowed()
    {
        Stored().Ignored = true;

        Assert.True((await Allocate()).IsSuccess);
    }

    private Task<Result> SetIgnored(bool ignored)
    {
        return new SetBankTransactionIgnoredCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new SetBankTransactionIgnoredCommand(7, ignored), CancellationToken.None);
    }

    [Fact]
    public async Task Ignored_IsSetAndCleared()
    {
        BankTransaction bankTransaction = Stored();

        Assert.True((await SetIgnored(true)).IsSuccess);
        Assert.True(bankTransaction.Ignored);
        Assert.True((await SetIgnored(false)).IsSuccess);
        Assert.False(bankTransaction.Ignored);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Ignored_Errors()
    {
        Assert.Equal(BankTransactionErrors.BankTransactionNotFound.Code, (await SetIgnored(true)).Error.Code);

        Stored(new Payment { StudentContractId = 11, Amount = 1m });
        Assert.Equal(BankTransactionErrors.BankTransactionHasPayments.Code, (await SetIgnored(true)).Error.Code);
        // clearing the mark of an allocated transaction is harmless
        Assert.True((await SetIgnored(false)).IsSuccess);
    }

    [Theory]
    [InlineData(null, EBankTransactionsFilter.None)]
    [InlineData(" ", EBankTransactionsFilter.None)]
    [InlineData("unallocated", EBankTransactionsFilter.Unallocated)]
    [InlineData("IGNORED", EBankTransactionsFilter.Ignored)]
    public async Task GetList_PassesTheFilterAndTheDays(string? filter, EBankTransactionsFilter expected)
    {
        List<BankTransactionRowResponse> rows = [];
        _repository.Setup(r => r.GetRows(Today, Today.AddDays(1), expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        Result<List<BankTransactionRowResponse>> result = await new GetBankTransactionsQueryHandler(_repository.Object)
            .Handle(new GetBankTransactionsQuery(Today.AddHours(5), Today.AddDays(1).AddHours(1), filter),
                CancellationToken.None);

        Assert.Same(rows, result.Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("all")]
    public async Task GetList_UnknownFilter_IsInvalid(string filter)
    {
        Result<List<BankTransactionRowResponse>> result = await new GetBankTransactionsQueryHandler(_repository.Object)
            .Handle(new GetBankTransactionsQuery(null, null, filter), CancellationToken.None);

        Assert.Equal(BankTransactionErrors.ListFilterIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetList_ReversedPeriod_IsInvalid()
    {
        Result<List<BankTransactionRowResponse>> result = await new GetBankTransactionsQueryHandler(_repository.Object)
            .Handle(new GetBankTransactionsQuery(Today, Today.AddDays(-1), null), CancellationToken.None);

        Assert.Equal(BankTransactionErrors.PeriodIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetOne_FoundOrNotFound()
    {
        BankTransactionResponse response = new(7, "თიბისი", Today, 10m, "GEL", null, null, null, null, null, null, null, 0,
            false, false, []);
        _repository.Setup(r => r.GetOne(7, It.IsAny<CancellationToken>())).ReturnsAsync(response);
        GetBankTransactionQueryHandler handler = new(_repository.Object);

        Assert.Same(response, (await handler.Handle(new GetBankTransactionQuery(7), CancellationToken.None)).Value);
        Assert.Equal(BankTransactionErrors.BankTransactionNotFound.Code,
            (await handler.Handle(new GetBankTransactionQuery(8), CancellationToken.None)).Error.Code);
    }

    [Fact]
    public async Task StudentContracts_AreThePaymentFormsList()
    {
        List<LookupItemResponse> contracts = [new(11, "ბერიძე ნინო 6.001")];
        _payments.Setup(p => p.GetStudentContracts(6, It.IsAny<CancellationToken>())).ReturnsAsync(contracts);

        Result<List<LookupItemResponse>> result =
            await new GetBankTransactionStudentContractsQueryHandler(_payments.Object).Handle(
                new GetBankTransactionStudentContractsQuery(6), CancellationToken.None);

        Assert.Same(contracts, result.Value);
    }

    [Fact]
    public async Task UnallocatedReport_ReadsTheUnallocatedRows()
    {
        _repository.Setup(r => r.GetRows(null, null, EBankTransactionsFilter.Unallocated,
            It.IsAny<CancellationToken>())).ReturnsAsync([
            new BankTransactionRowResponse(1, "თიბისი", Today, 10m, "p", "s", 0, false, false, 0m, false)
        ]);

        Result<ReportTable> result = await new UnallocatedBankTransactionsReportQueryHandler(_repository.Object)
            .Handle(new UnallocatedBankTransactionsReportQuery(), CancellationToken.None);

        Assert.Single(Assert.Single(result.Value.Sections).Rows);
    }
}
