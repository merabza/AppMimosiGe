using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class BankTransactionsRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly BankTransactionsRepository _repository;

    public BankTransactionsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new BankTransactionsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //students 1 (Alpha Ann) and 2 (Beta Bob), payer 3 (Gamma Gia); contracts 10 (1, 6.001) and 11 (2, 6.002) of
    //2026-2027, 12 (1, 5.001) of 2025-2026; account 4. Transactions: 20 (01.11, 100: payments 60 to 10 and 40 to 11,
    //one checked), 21 (02.11, 50: payment 20 to 11), 22 (03.11 10:00, 30: none), 23 (03.11, 70, ignored), 24 of
    //account 5. Synthetic people only: never real names
    private void Seed()
    {
        _context.AcademicYears.AddRange(
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = At(9, 1).AddYears(-1), FinishDate = At(9, 1)
            },
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = At(9, 1), FinishDate = At(9, 1).AddYears(1)
            });
        _context.Humans.AddRange(Human(1, "Alpha", "Ann"), Human(2, "Beta", "Bob"), Human(3, "Gamma", "Gia"));
        _context.StudentContracts.AddRange(Contract(10, "6.001", 1, 11), Contract(11, "6.002", 2, 11),
            Contract(12, " 5.001 ", 1, 10));
        _context.BankAccounts.AddRange(
            new BankAccount { BaId = 4, BankName = "Alpha Bank", BankCode = "B4", AccountNumber = "GE4" },
            new BankAccount { BaId = 5, BankName = "Beta Bank", BankCode = "B5", AccountNumber = "GE5" });
        _context.BankTransactions.AddRange(Transaction(20, "m20", At(11, 1), 100m),
            Transaction(21, "m21", At(11, 2), 50m), Transaction(22, "m22", At(11, 3, 10), 30m),
            Transaction(23, "m23", At(11, 3), 70m, true), Transaction(24, "m20", At(11, 4), 10m, bankAccountId: 5));
        _context.Payments.AddRange(Payment(30, 10, 20, 60m, true), Payment(31, 11, 20, 40m, false),
            Payment(32, 11, 21, 20m, false), Payment(33, 10, null, 5m, false));
        _context.StudentPayers.AddRange(
            new StudentPayer { Id = 1, StudentHumanId = 1, PersonalNumber = "01001000100", PayerName = "Gamma Gia" },
            new StudentPayer { Id = 2, StudentHumanId = 2, AccountIban = "GE00TB0000000000000100" });
        _context.SaveChanges();
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };
    }

    private static StudentContract Contract(int id, string number, int studentId, int academicYearId)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = studentId,
            PayerHumanId = 3,
            AcademicYearId = academicYearId
        };
    }

    private static BankTransaction Transaction(int id, string movementId, DateTime date, decimal amount,
        bool ignored = false, int bankAccountId = 4)
    {
        return new BankTransaction
        {
            Id = id,
            BankAccountId = bankAccountId,
            MovementId = movementId,
            TransactionDate = date,
            Amount = amount,
            Currency = "GEL",
            Purpose = $"purpose {id}",
            PartnerName = $"payer {id}",
            PartnerTaxCode = "01001000100",
            PartnerAccountIban = "GE00TB0000000000000100",
            DocumentNumber = $"doc {id}",
            MatchStep = id % 3,
            AllocatedManually = id == 21,
            Ignored = ignored
        };
    }

    private static Payment Payment(int id, int studentContractId, int? bankTransactionId, decimal amount,
        bool isChecked)
    {
        return new Payment
        {
            Id = id,
            StudentContractId = studentContractId,
            BankTransactionId = bankTransactionId,
            PayDate = At(11, 1),
            Amount = amount,
            BankAccountId = 4,
            Checked = isChecked
        };
    }

    [Fact]
    public async Task GetAcademicYears_All()
    {
        Assert.Equal([10, 11], (await _repository.GetAcademicYears()).Select(a => a.AyId).Order());
    }

    [Fact]
    public async Task GetLastTransactionDate_OfTheAccount()
    {
        Assert.Equal(At(11, 3, 10), await _repository.GetLastTransactionDate(4));
        Assert.Equal(At(11, 4), await _repository.GetLastTransactionDate(5));
        Assert.Null(await _repository.GetLastTransactionDate(6));
    }

    [Fact]
    public async Task GetExistingMovementIds_OfTheAccount()
    {
        Assert.Equal(["m20", "m22"], (await _repository.GetExistingMovementIds(4, ["m20", "m22", "m99"])).Order());
        Assert.Equal(["m20"], await _repository.GetExistingMovementIds(5, ["m20", "m21"]));
    }

    [Fact]
    public async Task GetMatchContracts_TheYearsContractsWithStudentAndPayer()
    {
        Assert.Equal([
            new MatchContractData(10, 1, "Ann", "Alpha", 3, "01000000003", "Gia", "Gamma"),
            new MatchContractData(11, 2, "Bob", "Beta", 3, "01000000003", "Gia", "Gamma")
        ], await _repository.GetMatchContracts(11));
    }

    [Fact]
    public async Task GetStudentByContractNumber_AllYearsTrimmed()
    {
        Assert.Equal(new Dictionary<string, int> { ["6.001"] = 1, ["6.002"] = 2, ["5.001"] = 1 },
            await _repository.GetStudentByContractNumber());
    }

    [Fact]
    public async Task GetLearnedPayers_All()
    {
        Assert.Equal([new LearnedPayer(1, "01001000100", null), new LearnedPayer(2, null, "GE00TB0000000000000100")],
            await _repository.GetLearnedPayers());
    }

    [Fact]
    public async Task GetRows_LatestFirstWithAllocatedSums()
    {
        List<BankTransactionRowResponse> rows = await _repository.GetRows(null, null, EBankTransactionsFilter.None);

        Assert.Equal([24, 22, 23, 21, 20], rows.Select(r => r.Id));
        Assert.Equal(new BankTransactionRowResponse(20, At(11, 1), 100m, "payer 20", "purpose 20", 2, false, false,
            100m, true), rows[4]);
        Assert.Equal((20m, false, true), (rows[3].AllocatedAmount, rows[3].HasCheckedPayments,
            rows[3].AllocatedManually));
        Assert.Equal(0m, rows[1].AllocatedAmount);
    }

    [Fact]
    public async Task GetRows_Filters()
    {
        Assert.Equal([24, 22, 21], (await _repository.GetRows(null, null, EBankTransactionsFilter.Unallocated))
            .Select(r => r.Id));
        Assert.Equal([23], (await _repository.GetRows(null, null, EBankTransactionsFilter.Ignored)).Select(r => r.Id));
        // both days included, whatever the time
        Assert.Equal([22, 23, 21], (await _repository.GetRows(At(11, 2), At(11, 3), EBankTransactionsFilter.None))
            .Select(r => r.Id));
    }

    [Fact]
    public async Task GetOne_WithTheAllocation()
    {
        BankTransactionResponse? response = await _repository.GetOne(20);

        Assert.NotNull(response);
        Assert.Equal((20, At(11, 1), 100m, "GEL", "purpose 20", "doc 20", "payer 20", "GE00TB0000000000000100",
            "01001000100", 2, false, false), (response.Id, response.TransactionDate, response.Amount, response.Currency,
            response.Purpose, response.DocumentNumber, response.PartnerName, response.PartnerAccountIban,
            response.PartnerTaxCode, response.MatchStep, response.AllocatedManually, response.Ignored));
        Assert.Equal([
            new BankTransactionAllocationResponse(30, 10, "Alpha Ann 6.001", 11, 60m, true),
            new BankTransactionAllocationResponse(31, 11, "Beta Bob 6.002", 11, 40m, false)
        ], response.Allocations);
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_TrackedWithPayments()
    {
        BankTransaction? bankTransaction = await _repository.GetForChange(20);

        Assert.NotNull(bankTransaction);
        Assert.Equal([30, 31], bankTransaction.Payments.Select(p => p.Id).Order());
        Assert.Equal(EntityState.Unchanged, _context.Entry(bankTransaction).State);
        Assert.Null(await _repository.GetForChange(99));
    }

    [Fact]
    public async Task GetStudentHumanIds_OfTheContracts()
    {
        Assert.Equal(new Dictionary<int, int> { [10] = 1, [12] = 1 }, await _repository.GetStudentHumanIds([10, 12, 99]));
    }

    [Fact]
    public async Task GetStudentPayers_OfTheStudents()
    {
        Assert.Equal([2], (await _repository.GetStudentPayers([2, 9])).Select(p => p.Id));
    }

    [Fact]
    public void AddAndRemove_ChangeTheContext()
    {
        BankTransaction bankTransaction = Transaction(25, "m25", At(11, 5), 5m);
        StudentPayer studentPayer = new() { Id = 3, StudentHumanId = 1, AccountIban = "X" };
        Payment payment = _context.Payments.Single(p => p.Id == 32);

        _repository.Add(bankTransaction);
        _repository.AddStudentPayer(studentPayer);
        _repository.RemovePayment(payment);

        Assert.Equal(EntityState.Added, _context.Entry(bankTransaction).State);
        Assert.Equal(EntityState.Added, _context.Entry(studentPayer).State);
        Assert.Equal(EntityState.Deleted, _context.Entry(payment).State);
    }
}
