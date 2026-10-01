using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class PaymentsRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly PaymentsRepository _repository;

    public PaymentsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new PaymentsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //contracts 10 (Alpha Ann 6.001), 11 (Beta Bob 6.002) and 13 (Gamma Gia 6.003) of 2026-2027, 12 (Gamma Gia 6.001)
    //of 2025-2026; payment types 1 Zeta Bank, 4 Alpha Bank, 8 Last Year. Payments: 1 (10, 03.09, 100, checked),
    //2 (11, 03.09, 50.25), 3 (10, 15.09 18:00, -20, a transfer), 4 (12, 31.08, 75, checked), 5 (11, 30.09, 10.5, no
    //type). Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
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
        _context.StudentContracts.AddRange(StudentContract(10, "6.001", 1, 11), StudentContract(11, "6.002", 2, 11),
            StudentContract(12, "6.001", 3, 10), StudentContract(13, "6.003", 3, 11));
        _context.BankAccounts.AddRange(BankAccount(1, "Zeta Bank"), BankAccount(4, "Alpha Bank"),
            BankAccount(8, "Last Year"));
        _context.Payments.AddRange(Payment(1, 10, At(9, 3), 100m, "d1", 1, true),
            Payment(2, 11, At(9, 3), 50.25m, null, 4, false), Payment(3, 10, At(9, 15, 18), -20m, "transfer", 8, false),
            Payment(4, 12, At(8, 31), 75m, "old", 1, true), Payment(5, 11, At(9, 30), 10.5m, "d5", null, false));
        _context.SaveChanges();
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };
    }

    private static StudentContract StudentContract(int id, string number, int studentId, int academicYearId)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = studentId,
            PayerHumanId = studentId,
            AcademicYearId = academicYearId,
            DirtyNextPayDate = false
        };
    }

    private static BankAccount BankAccount(int id, string name)
    {
        return new BankAccount { BaId = id, BankName = name, BankCode = $"B{id}", AccountNumber = $"GE{id}" };
    }

    private static Payment Payment(int id, int studentContractId, DateTime payDate, decimal amount, string? document,
        int? bankAccountId, bool isChecked)
    {
        return new Payment
        {
            Id = id,
            StudentContractId = studentContractId,
            PayDate = payDate,
            Amount = amount,
            Document = document,
            BankAccountId = bankAccountId,
            Checked = isChecked
        };
    }

    private static PaymentsListQuery Query(int? studentContractId = null, int? bankAccountId = null,
        DateTime? dateFrom = null, DateTime? dateTo = null, IReadOnlyList<PaymentSortField>? sortFields = null,
        int offset = 0, int rowsCount = 100)
    {
        return new PaymentsListQuery(offset, rowsCount, studentContractId, bankAccountId, dateFrom, dateTo,
            sortFields ?? PaymentsListQueryFactory.DefaultSortFields);
    }

    private async Task AssertIds(PaymentsListQuery query, params int[] expected)
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(query);
        Assert.Equal(expected, data.Rows.Select(r => r.Id));
    }

    private async Task AssertTotal(PaymentsListQuery query, decimal expected)
    {
        Assert.Equal(expected, (await _repository.GetRowsData(query)).TotalAmount);
    }

    [Fact]
    public async Task GetRowsData_ListsAllPaymentsByDateThenStudentWithTheirSum()
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(0, data.Offset);
        Assert.Equal(215.75m, data.TotalAmount);
        Assert.Equal([4, 1, 2, 3, 5], data.Rows.Select(r => r.Id));
    }

    // the student is "last name first name contract number", as in the Access combo box
    [Fact]
    public async Task GetRowsData_RowHasStudentTypeAndCheckedFlag()
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(new PaymentRowResponse(1, 10, "Alpha Ann 6.001", At(9, 3), 100m, "d1", 1, "Zeta Bank", true),
            data.Rows.Single(r => r.Id == 1));
        Assert.Equal(new PaymentRowResponse(5, 11, "Beta Bob 6.002", At(9, 30), 10.5m, "d5", null, null, false),
            data.Rows.Single(r => r.Id == 5));
    }

    [Fact]
    public async Task GetRowsData_FiltersByContract()
    {
        await AssertIds(Query(10), 1, 3);
        await AssertTotal(Query(10), 80m);
    }

    [Fact]
    public async Task GetRowsData_FiltersByPaymentType()
    {
        await AssertIds(Query(bankAccountId: 1), 4, 1);
        await AssertTotal(Query(bankAccountId: 1), 175m);
    }

    // both ends of the range are whole days: the 18:00 payment of 15.09 is in "until 15.09"
    [Fact]
    public async Task GetRowsData_FiltersByDaysIncludingBothEnds()
    {
        await AssertIds(Query(dateFrom: At(9, 3), dateTo: At(9, 15)), 1, 2, 3);
        await AssertIds(Query(dateFrom: At(9, 15)), 3, 5);
        await AssertIds(Query(dateTo: At(9, 3)), 4, 1, 2);
        await AssertTotal(Query(dateFrom: At(9, 1), dateTo: At(9, 30)), 140.75m);
    }

    [Fact]
    public async Task GetRowsData_CombinesTheFilters()
    {
        await AssertIds(Query(10, 1, At(9, 1), At(9, 30)), 1);
        await AssertTotal(Query(10, 1, At(9, 1), At(9, 30)), 100m);
    }

    // the sum is that of the whole filter, not of the page
    [Fact]
    public async Task GetRowsData_PageHasTheSumOfTheWholeFilter()
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(2, data.Offset);
        Assert.Equal(215.75m, data.TotalAmount);
        Assert.Equal([2, 3], data.Rows.Select(r => r.Id));
    }

    // a page that no longer exists after a filter change shows the last page
    [Fact]
    public async Task GetRowsData_OffsetBeyondTheRows_ShowsTheLastPage()
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 10, rowsCount: 2));

        Assert.Equal(4, data.Offset);
        Assert.Equal([5], data.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRowsData_NothingFound_HasZeroSum()
    {
        PaymentsRowsDataResponse data = await _repository.GetRowsData(Query(13));

        Assert.Equal(0, data.AllRowsCount);
        Assert.Equal(0, data.Offset);
        Assert.Equal(0m, data.TotalAmount);
        Assert.Empty(data.Rows);
    }

    // ties are broken by ID; an empty document and payment type come first
    [Theory]
    [InlineData(EPaymentSortField.PayDate, false, new[] { 5, 3, 1, 2, 4 })]
    [InlineData(EPaymentSortField.StudentName, false, new[] { 4, 2, 5, 1, 3 })]
    [InlineData(EPaymentSortField.Amount, false, new[] { 1, 4, 2, 5, 3 })]
    [InlineData(EPaymentSortField.Amount, true, new[] { 3, 5, 2, 4, 1 })]
    [InlineData(EPaymentSortField.Document, true, new[] { 2, 1, 5, 4, 3 })]
    [InlineData(EPaymentSortField.BankName, true, new[] { 5, 2, 3, 1, 4 })]
    [InlineData(EPaymentSortField.Checked, false, new[] { 1, 4, 2, 3, 5 })]
    public async Task GetRowsData_SortsByTheField(EPaymentSortField field, bool ascending, int[] expected)
    {
        await AssertIds(Query(sortFields: [new PaymentSortField(field, ascending)]), expected);
    }

    [Fact]
    public async Task GetOne_ReturnsThePaymentWithItsContractYear()
    {
        Assert.Equal(new PaymentResponse(3, 10, "Alpha Ann 6.001", 11, At(9, 15, 18), -20m, "transfer", 8, false),
            await _repository.GetOne(3));
        Assert.Equal(new PaymentResponse(4, 12, "Gamma Gia 6.001", 10, At(8, 31), 75m, "old", 1, true),
            await _repository.GetOne(4));
    }

    [Fact]
    public async Task GetOne_MissingPayment_IsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_ReturnsTheTrackedPayment()
    {
        Payment? payment = await _repository.GetForChange(2);

        Assert.NotNull(payment);
        Assert.Equal(50.25m, payment.Amount);
        Assert.Equal(EntityState.Unchanged, _context.Entry(payment).State);
        Assert.Null(await _repository.GetForChange(99));
    }

    [Fact]
    public async Task StudentContractExists_FindsAnyYearsContract()
    {
        Assert.True(await _repository.StudentContractExists(12));
        Assert.False(await _repository.StudentContractExists(99));
    }

    [Fact]
    public async Task BankAccountExists_FindsThePaymentType()
    {
        Assert.True(await _repository.BankAccountExists(8));
        Assert.False(await _repository.BankAccountExists(2));
    }

    [Fact]
    public async Task GetBankAccounts_AreSortedByName()
    {
        Assert.Equal([new LookupItemResponse(4, "Alpha Bank"), new LookupItemResponse(8, "Last Year"),
            new LookupItemResponse(1, "Zeta Bank")], await _repository.GetBankAccounts());
    }

    [Fact]
    public async Task GetStudentContracts_ListsTheYearsContractsByName()
    {
        Assert.Equal([new LookupItemResponse(10, "Alpha Ann 6.001"), new LookupItemResponse(11, "Beta Bob 6.002"),
            new LookupItemResponse(13, "Gamma Gia 6.003")], await _repository.GetStudentContracts(11));
        Assert.Equal([new LookupItemResponse(12, "Gamma Gia 6.001")], await _repository.GetStudentContracts(10));
        Assert.Empty(await _repository.GetStudentContracts(99));
    }

    [Fact]
    public async Task GetStudentContractsForChange_ReturnsTheTrackedContracts()
    {
        List<StudentContract> contracts = await _repository.GetStudentContractsForChange([12, 10]);

        Assert.Equal([10, 12], contracts.Select(c => c.ScId).Order());
        Assert.All(contracts, c => Assert.Equal(EntityState.Unchanged, _context.Entry(c).State));
    }

    [Fact]
    public void Add_StoresThePaymentOnSave()
    {
        _repository.Add(Payment(0, 13, At(10, 1), 30m, null, 4, false));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        Assert.Equal(30m, _context.Payments.Single(p => p.StudentContractId == 13).Amount);
    }

    [Fact]
    public void Remove_DeletesThePaymentOnSave()
    {
        _repository.Remove(_context.Payments.Single(p => p.Id == 2));
        _context.SaveChanges();

        Assert.False(_context.Payments.Any(p => p.Id == 2));
        Assert.Equal(4, _context.Payments.Count());
    }
}
