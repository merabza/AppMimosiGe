using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class TeacherContractsRepositoryTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly MimosiGeDbContext _context;
    private readonly TeacherContractsRepository _repository;

    public TeacherContractsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new TeacherContractsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    //synthetic people only: never real names
    private void Seed()
    {
        _context.Humans.AddRange(Human(1, "Alpha", "Ann", "01000000001"), Human(2, "Beta", "Bob", "01000000002"),
            Human(3, "Gamma", "Gia", "02000000003"));
        _context.RsCountries.AddRange(new RsCountry { Id = 1, Code = "GE", CountryName = "Georgia" },
            new RsCountry { Id = 2, Code = "AM", CountryName = "Armenia" });
        _context.RsQuoteTypes.AddRange(new RsQuoteType { QtId = 1, QtName = "Salary" },
            new RsQuoteType { QtId = 21, QtName = "Other" });
        _context.TeacherSalarySchemes.AddRange(new TeacherSalaryScheme { Id = 1, SchemaName = "Senior" },
            new TeacherSalaryScheme { Id = 2, SchemaName = "Junior" });
        _context.WorkHourGroups.AddRange(new WorkHourGroup { WhgId = 1, WhgKey = "Z", WhgName = "Admin" },
            new WorkHourGroup { WhgId = 2, WhgKey = "A", WhgName = "Media" });
        _context.TeacherContracts.AddRange(Contract(1, "T3.02", 2, 1, null),
            Contract(2, "T3.01", 1, 2, Today.AddDays(-1)), Contract(3, "T4.01", 3, null, Today),
            Contract(4, "T3.03", 3, 1, Today.AddDays(10)));
        _context.SaveChanges();
    }

    //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
    //so test data is saved synchronously
    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
    }

    private static Human Human(int id, string lastName, string firstName, string personalId)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = personalId };
    }

    private static TeacherContract Contract(int id, string number, int teacherId, int? schemeId, DateTime? endDate)
    {
        return new TeacherContract
        {
            Id = id,
            ContractNumber = number,
            ContractDate = new DateTime(2025, 9, id, 0, 0, 0, DateTimeKind.Unspecified),
            TeacherHumanId = teacherId,
            RsCountryId = 1,
            SalarySchemaByHoursId = schemeId,
            ContractEndDate = endDate,
            FixedAmount = id * 100
        };
    }

    private static TeacherContractsListQuery Query(DateTime? activeOn = null, string? search = null,
        IReadOnlyList<TeacherContractSortField>? sort = null, int offset = 0, int rowsCount = 10)
    {
        return new TeacherContractsListQuery(offset, rowsCount, activeOn, search,
            sort ?? TeacherContractsListQueryFactory.DefaultSortFields);
    }

    [Fact]
    public async Task GetRowsData_ListsAllSortedLikeAccess()
    {
        TeacherContractsRowsDataResponse result = await _repository.GetRowsData(Query());

        Assert.Equal(4, result.AllRowsCount);
        Assert.Equal([2, 1, 4, 3], result.Rows.Select(r => r.Id));
        TeacherContractRowResponse first = result.Rows[0];
        Assert.Equal("T3.01", first.ContractNumber);
        Assert.Equal("Alpha Ann", first.TeacherName);
        Assert.Equal("Junior", first.SalarySchemeName);
        Assert.Equal(200m, first.FixedAmount);
        Assert.Equal(Today.AddDays(-1), first.ContractEndDate);
        Assert.Null(result.Rows[3].SalarySchemeName);
    }

    // a contract that ends today is still active today
    [Fact]
    public async Task GetRowsData_ActiveOn_HidesContractsThatEndedBefore()
    {
        TeacherContractsRowsDataResponse result = await _repository.GetRowsData(Query(Today));

        Assert.Equal(3, result.AllRowsCount);
        Assert.Equal([1, 4, 3], result.Rows.Select(r => r.Id));
    }

    [Theory]
    [InlineData("T3.0", new[] { 2, 1, 4 })]
    [InlineData("Gamma Gia", new[] { 4, 3 })]
    [InlineData("Gia Gamma", new[] { 4, 3 })]
    [InlineData("Beta", new[] { 1 })]
    [InlineData("nobody", new int[0])]
    public async Task GetRowsData_SearchesNumberAndTeacherName(string search, int[] expectedIds)
    {
        TeacherContractsRowsDataResponse result = await _repository.GetRowsData(Query(search: search));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.Id));
    }

    [Theory]
    [InlineData(ETeacherContractSortField.ContractDate, false, new[] { 4, 3, 2, 1 })]
    [InlineData(ETeacherContractSortField.TeacherName, true, new[] { 2, 1, 3, 4 })]
    [InlineData(ETeacherContractSortField.SalarySchemeName, true, new[] { 3, 2, 1, 4 })]
    [InlineData(ETeacherContractSortField.FixedAmount, false, new[] { 4, 3, 2, 1 })]
    [InlineData(ETeacherContractSortField.ContractEndDate, true, new[] { 1, 2, 3, 4 })]
    [InlineData(ETeacherContractSortField.ContractNumber, false, new[] { 3, 4, 1, 2 })]
    public async Task GetRowsData_SortsByTheRequestedField(ETeacherContractSortField field, bool ascending,
        int[] expectedIds)
    {
        TeacherContractsRowsDataResponse result =
            await _repository.GetRowsData(Query(sort: [new TeacherContractSortField(field, ascending)]));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.Id));
    }

    // equal values fall back to the id, so paging is stable
    [Theory]
    [InlineData(ETeacherContractSortField.PensionScheme)]
    [InlineData(ETeacherContractSortField.IndEnt)]
    public async Task GetRowsData_EqualValues_AreOrderedById(ETeacherContractSortField field)
    {
        TeacherContractsRowsDataResponse result =
            await _repository.GetRowsData(Query(sort: [new TeacherContractSortField(field, true)]));

        Assert.Equal([1, 2, 3, 4], result.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRowsData_ReturnsTheRequestedPage()
    {
        TeacherContractsRowsDataResponse result = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(4, result.AllRowsCount);
        Assert.Equal(2, result.Offset);
        Assert.Equal([4, 3], result.Rows.Select(r => r.Id));
    }

    // after a filter change the page may no longer exist, then the last page is shown
    [Fact]
    public async Task GetRowsData_OffsetBeyondTheEnd_ReturnsTheLastPage()
    {
        TeacherContractsRowsDataResponse result = await _repository.GetRowsData(Query(offset: 10, rowsCount: 3));

        Assert.Equal(3, result.Offset);
        Assert.Equal([3], result.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetOne_ReturnsAllFieldsWithTimesOnly()
    {
        AddAndSave(new TeacherContract
        {
            Id = 9,
            ContractNumber = "T6.01",
            ContractDate = Today,
            TeacherHumanId = 2,
            BankAccount = "GE00TB0000000000000000",
            BankAccountCode = "TBCBGE22",
            PensionScheme = true,
            IndEnt = true,
            RsQuoteTypeId = 21,
            RsCountryId = 2,
            FixedAmount = 1234.5m,
            NextMonth = true,
            Description = "Media",
            SalarySchemaByHoursId = 1,
            WorkHourGroupId = 2,
            WorkHoursStart = new DateTime(1899, 12, 30, 12, 0, 0, DateTimeKind.Unspecified),
            WorkHoursEnd = new DateTime(1899, 12, 30, 18, 30, 0, DateTimeKind.Unspecified),
            ContractEndDate = Today.AddYears(1)
        });

        TeacherContractResponse? result = await _repository.GetOne(9);

        Assert.Equal(new TeacherContractResponse(9, "T6.01", Today, 2, "Beta Bob", "GE00TB0000000000000000",
            "TBCBGE22", true, true, 21, 2, 1234.5m, true, "Media", 1, 2, new TimeOnly(12, 0), new TimeOnly(18, 30),
            Today.AddYears(1)), result);
    }

    [Fact]
    public async Task GetOne_EmptyTimes_AreNull()
    {
        TeacherContractResponse? result = await _repository.GetOne(1);

        Assert.NotNull(result);
        Assert.Null(result.WorkHoursStart);
        Assert.Null(result.WorkHoursEnd);
    }

    [Fact]
    public async Task GetOne_Missing_ReturnsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_ReturnsTrackedContract()
    {
        TeacherContract? result = await _repository.GetForChange(1);

        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, _context.Entry(result).State);
        Assert.Null(await _repository.GetForChange(99));
    }

    // the number is unique among all contracts; the edited one is excluded
    [Theory]
    [InlineData("T3.01", 0, true)]
    [InlineData("T3.01", 2, false)]
    [InlineData("T3.01", 1, true)]
    [InlineData("T9.99", 0, false)]
    public async Task ContractNumberExists_ChecksAllContractsExceptTheEdited(string number, int exceptId,
        bool expected)
    {
        Assert.Equal(expected, await _repository.ContractNumberExists(number, exceptId));
    }

    [Fact]
    public async Task IsInUse_FreeContract_IsFalse()
    {
        Assert.False(await _repository.IsInUse(1));
    }

    [Fact]
    public async Task IsInUse_GroupTeacher_IsTrue()
    {
        AddAndSave(new GroupByTeacher { Id = 1, GroupId = 1, TeacherContractId = 1, SalarySchemaId = 1 });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task IsInUse_LessonTeacher_IsTrue()
    {
        AddAndSave(new Lesson { Id = 1, GroupId = 1, TeacherContractId = 1, SalarySchemaId = 1 });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task IsInUse_LessonSubstitute_IsTrue()
    {
        AddAndSave(new Lesson
        {
            Id = 1, GroupId = 1, TeacherContractId = 2, SubstituteTeacherContractId = 1, SalarySchemaId = 1
        });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(3));
    }

    [Fact]
    public async Task IsInUse_SalaryPart_IsTrue()
    {
        AddAndSave(new SalaryPart { SpId = 1, ShId = 1, TeacherContractId = 1 });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task IsInUse_SalaryLine_IsTrue()
    {
        AddAndSave(new SalaryLine { SaId = 1, ShId = 1, TeacherContractId = 1 });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task IsInUse_WorkHours_IsTrue()
    {
        AddAndSave(new WorkHour { WhId = 1, TeacherContractId = 1, WhStart = Today });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task ExistsChecks_FindOnlyExistingRecords()
    {
        Assert.True(await _repository.HumanExists(1));
        Assert.False(await _repository.HumanExists(99));
        Assert.True(await _repository.RsQuoteTypeExists(21));
        Assert.False(await _repository.RsQuoteTypeExists(2));
        Assert.True(await _repository.RsCountryExists(2));
        Assert.False(await _repository.RsCountryExists(99));
        Assert.True(await _repository.SalarySchemeExists(2));
        Assert.False(await _repository.SalarySchemeExists(99));
        Assert.True(await _repository.WorkHourGroupExists(2));
        Assert.False(await _repository.WorkHourGroupExists(99));
    }

    [Fact]
    public async Task Lookups_AreSortedByTheirShownName()
    {
        Assert.Equal([new LookupItemResponse(21, "Other"), new LookupItemResponse(1, "Salary")],
            await _repository.GetRsQuoteTypes());
        Assert.Equal([new LookupItemResponse(2, "Armenia"), new LookupItemResponse(1, "Georgia")],
            await _repository.GetRsCountries());
        Assert.Equal([new LookupItemResponse(2, "Junior"), new LookupItemResponse(1, "Senior")],
            await _repository.GetSalarySchemes());
        Assert.Equal([new LookupItemResponse(2, "A"), new LookupItemResponse(1, "Z")],
            await _repository.GetWorkHourGroups());
    }

    [Fact]
    public void AddAndRemove_ChangeTheContext()
    {
        var added = new TeacherContract { Id = 10, ContractNumber = "T9.01", RsCountryId = 1, TeacherHumanId = 1 };
        _repository.Add(added);
        Assert.Equal(EntityState.Added, _context.Entry(added).State);

        TeacherContract existing = _context.TeacherContracts.Single(tc => tc.Id == 1);
        _repository.Remove(existing);
        Assert.Equal(EntityState.Deleted, _context.Entry(existing).State);
    }
}
