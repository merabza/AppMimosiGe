using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class StudentContractsRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly StudentContractsRepository _repository;

    public StudentContractsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new StudentContractsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    //synthetic people only: never real names
    private void Seed()
    {
        _context.AcademicYears.AddRange(
            new AcademicYear
            {
                AyId = 10,
                AcademicYearName = "2025-2026",
                StartDate = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            },
            new AcademicYear
            {
                AyId = 11,
                AcademicYearName = "2026-2027",
                StartDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            });
        _context.StudentStatuses.AddRange(new StudentStatus { Id = 1, StudentStatusName = "Z-class", Rate = 312 },
            new StudentStatus { Id = 2, StudentStatusName = "A-class", Rate = 101 });
        _context.Courses.AddRange(new Course { CrsId = 1, CourseName = "Math" },
            new Course { CrsId = 2, CourseName = "Art" });
        _context.GroupSizes.AddRange(new GroupSize { GrsId = 2, GrsSize = 4, GrsName = "Group" },
            new GroupSize { GrsId = 1, GrsSize = 1, GrsName = "Single" });
        _context.Humans.AddRange(Human(1, "Alpha", "Ann", "01000000001"), Human(2, "Beta", "Bob", "01000000002"),
            Human(3, "Gamma", "Gia", "02000000003"), Human(4, "Delta", "Dan", "02000000004"));
        _context.StudentContracts.AddRange(
            Contract(1, "6.002", 11, 1, 2, 1, new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Unspecified)),
            Contract(2, "6.001", 11, 3, 4, 2, new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Unspecified)),
            Contract(3, "6.003", 11, 4, 4, null, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Unspecified)),
            Contract(4, "6.001", 10, 1, 2, 1, new DateTime(2025, 9, 5, 0, 0, 0, DateTimeKind.Unspecified)));
        _context.StudentContractDetails.AddRange(
            new StudentContractDetail
            {
                Id = 11,
                StudentContractId = 1,
                CourseId = 2,
                GroupSizeId = 1,
                FourWeekHours = 12,
                FourWeekFee = 72,
                OneHourFee = 6
            }, new StudentContractDetail { Id = 10, StudentContractId = 1, CourseId = 1, GroupSizeId = 2 });
        _context.Payments.Add(new Payment
        {
            Id = 1, StudentContractId = 2, PayDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Unspecified)
        });
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

    private static StudentContract Contract(int id, string number, int ayId, int studentId, int payerId, int? statusId,
        DateTime date)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            AcademicYearId = ayId,
            StudentHumanId = studentId,
            PayerHumanId = payerId,
            StudentStatusId = statusId,
            ContractDate = date
        };
    }

    private static StudentContractsListQuery Query(int? ayId = null, int? statusId = null, string? search = null,
        IReadOnlyList<StudentContractSortField>? sort = null, int offset = 0, int rowsCount = 10)
    {
        return new StudentContractsListQuery(offset, rowsCount, ayId, statusId, search,
            sort ?? StudentContractsListQueryFactory.DefaultSortFields);
    }

    [Fact]
    public async Task GetRowsData_FiltersByYearAndSortsLikeAccess()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(11));

        Assert.Equal(3, result.AllRowsCount);
        Assert.Equal([2, 1, 3], result.Rows.Select(r => r.ScId));
        StudentContractRowResponse first = result.Rows[0];
        Assert.Equal("6.001", first.ContractNumber);
        Assert.Equal("Gamma Gia", first.StudentName);
        Assert.Equal("Delta Dan", first.PayerName);
        Assert.Equal("2026-2027", first.AcademicYearName);
        Assert.Equal("A-class", first.StudentStatusName);
        Assert.Null(result.Rows[2].StudentStatusName);
    }

    [Fact]
    public async Task GetRowsData_FiltersByStatus()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(statusId: 1));

        Assert.Equal([4, 1], result.Rows.Select(r => r.ScId));
    }

    [Theory]
    [InlineData("6.00", new[] { 4, 2, 1, 3 })]
    [InlineData("Delta", new[] { 2, 3 })]
    [InlineData("Dan Delta", new[] { 2, 3 })]
    [InlineData("Alpha Ann", new[] { 4, 1 })]
    [InlineData("Bob", new[] { 4, 1 })]
    [InlineData("Gia Gamma", new[] { 2 })]
    [InlineData("Beta Bob", new[] { 4, 1 })]
    [InlineData("nobody", new int[0])]
    public async Task GetRowsData_SearchesNumberAndNames(string search, int[] expected)
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(search: search));

        Assert.Equal(expected, result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_SortsByRequestedFieldsWithIdTieBreaker()
    {
        StudentContractsRowsDataResponse byDateDesc = await _repository.GetRowsData(Query(11,
            sort: [new StudentContractSortField(EStudentContractSortField.ContractDate, false)]));
        StudentContractsRowsDataResponse byPayer = await _repository.GetRowsData(Query(11,
            sort: [new StudentContractSortField(EStudentContractSortField.PayerName, true)]));
        StudentContractsRowsDataResponse byStatus = await _repository.GetRowsData(Query(11,
            sort: [new StudentContractSortField(EStudentContractSortField.StudentStatusName, true)]));

        Assert.Equal([2, 3, 1], byDateDesc.Rows.Select(r => r.ScId));
        Assert.Equal([1, 2, 3], byPayer.Rows.Select(r => r.ScId));
        Assert.Equal([3, 2, 1], byStatus.Rows.Select(r => r.ScId));
    }

    [Theory]
    [InlineData(EStudentContractSortField.AcademicYearName, true, new[] { 4, 1, 2, 3 })]
    [InlineData(EStudentContractSortField.StudentName, false, new[] { 2, 3, 1, 4 })]
    [InlineData(EStudentContractSortField.ContractNumber, false, new[] { 3, 1, 2, 4 })]
    [InlineData(EStudentContractSortField.DesiredMonthlyPaymentDay, true, new[] { 1, 2, 3, 4 })]
    public async Task GetRowsData_SortsByEveryField(EStudentContractSortField field, bool ascending, int[] expected)
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(sort:
            [new StudentContractSortField(field, ascending)]));

        Assert.Equal(expected, result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_ReturnsRequestedPage()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(4, result.AllRowsCount);
        Assert.Equal(2, result.Offset);
        Assert.Equal([1, 3], result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_OffsetBeyondEnd_ReturnsLastPage()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(offset: 40, rowsCount: 3));

        Assert.Equal(3, result.Offset);
        Assert.Equal([3], result.Rows.Select(r => r.ScId));
    }

    // 4 rows, 2 per page: the last page starts at 2, whether the offset is just past the end or far beyond it
    [Theory]
    [InlineData(4)]
    [InlineData(40)]
    public async Task GetRowsData_OffsetAtOrBeyondEnd_ReturnsTheLastFullPage(int offset)
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(offset: offset, rowsCount: 2));

        Assert.Equal(2, result.Offset);
        Assert.Equal([1, 3], result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_SecondSortFieldDescending_IsApplied()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(sort:
        [
            new StudentContractSortField(EStudentContractSortField.AcademicYearName, true),
            new StudentContractSortField(EStudentContractSortField.ContractNumber, false)
        ]));

        Assert.Equal([4, 3, 1, 2], result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_UnknownSortField_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetRowsData(Query(sort: [new StudentContractSortField((EStudentContractSortField)99, true)])));

        Assert.Contains("უცნობი დალაგების ველი", exception.Message, StringComparison.Ordinal);
    }

    // names are sorted as "last first": the space sorts before any letter, so "A Z" comes before "AA A"
    [Theory]
    [InlineData(EStudentContractSortField.StudentName)]
    [InlineData(EStudentContractSortField.PayerName)]
    public async Task GetRowsData_SortsNamesAsLastSpaceFirst(EStudentContractSortField field)
    {
        AddAndSave(
            new AcademicYear
            {
                AyId = 12,
                AcademicYearName = "2027-2028",
                StartDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                FinishDate = new DateTime(2028, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            }, Human(11, "AA", "A", "03000000011"), Human(12, "A", "Z", "03000000012"),
            Contract(21, "7.001", 12, 11, 11, null, new DateTime(2027, 9, 2, 0, 0, 0, DateTimeKind.Unspecified)),
            Contract(22, "7.002", 12, 12, 12, null, new DateTime(2027, 9, 2, 0, 0, 0, DateTimeKind.Unspecified)));

        StudentContractsRowsDataResponse result =
            await _repository.GetRowsData(Query(12, sort: [new StudentContractSortField(field, true)]));

        Assert.Equal([22, 21], result.Rows.Select(r => r.ScId));
    }

    [Fact]
    public async Task GetRowsData_NoRows_KeepsOffset()
    {
        StudentContractsRowsDataResponse result = await _repository.GetRowsData(Query(search: "zzz", offset: 20));

        Assert.Equal(0, result.AllRowsCount);
        Assert.Equal(20, result.Offset);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task GetOne_ReturnsContractWithDetailsOrderedById()
    {
        StudentContractResponse? result = await _repository.GetOne(1);

        Assert.NotNull(result);
        Assert.Equal("6.002", result.ContractNumber);
        Assert.Equal("Alpha Ann", result.StudentName);
        Assert.Equal("Beta Bob", result.PayerName);
        Assert.True(result.DirtyNextPayDate);
        Assert.Equal([10, 11], result.Details.Select(d => d.Id));
        StudentContractDetailResponse detail = result.Details[1];
        Assert.Equal(12f, detail.FourWeekHours);
        Assert.Equal(72m, detail.FourWeekFee);
        Assert.Equal(6m, detail.OneHourFee);
    }

    [Fact]
    public async Task GetOne_Missing_ReturnsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_LoadsTrackedDetails()
    {
        StudentContract? result = await _repository.GetForChange(1);

        Assert.NotNull(result);
        Assert.Equal(2, result.StudentContractDetails.Count);
        Assert.Equal(EntityState.Unchanged, _context.Entry(result).State);
    }

    [Theory]
    [InlineData(11, "6.001", 0, true)]
    [InlineData(11, "6.001", 2, false)]
    [InlineData(10, "6.002", 0, false)]
    [InlineData(11, "6.009", 0, false)]
    public async Task ContractNumberExists_IsScopedToYearAndExcludesSelf(int ayId, string number, int exceptScId,
        bool expected)
    {
        Assert.Equal(expected, await _repository.ContractNumberExists(ayId, number, exceptScId));
    }

    [Fact]
    public async Task IsInUse_DetectsEachLinkedTable()
    {
        AddAndSave(new GroupByStudent { GbsId = 1, GroupId = 1, StudentContractId = 3 },
            new LessonByStudent { Id = 1, LessonId = 1, StudentContractId = 4 });

        Assert.False(await _repository.IsInUse(1));
        Assert.True(await _repository.IsInUse(2));
        Assert.True(await _repository.IsInUse(3));
        Assert.True(await _repository.IsInUse(4));
    }

    [Fact]
    public async Task IsInUse_DetectsCrmCall()
    {
        AddAndSave(new CrmCall { CcId = 1, StudentContractId = 1, CallTypeId = 1, AnswerTypeId = 1 });

        Assert.True(await _repository.IsInUse(1));
    }

    [Fact]
    public async Task ExistenceChecks_Work()
    {
        Assert.True(await _repository.HumanExists(1));
        Assert.False(await _repository.HumanExists(9));
        Assert.True(await _repository.AcademicYearExists(10));
        Assert.False(await _repository.AcademicYearExists(9));
        Assert.True(await _repository.StudentStatusExists(2));
        Assert.False(await _repository.StudentStatusExists(9));
        Assert.True(await _repository.CourseExists(2));
        Assert.False(await _repository.CourseExists(9));
        Assert.True(await _repository.GroupSizeExists(1));
        Assert.False(await _repository.GroupSizeExists(9));
    }

    [Fact]
    public async Task Lookups_AreOrderedLikeAccess()
    {
        Assert.Equal([2, 1], (await _repository.GetStudentStatuses()).Select(x => x.Id));
        Assert.Equal(["Art", "Math"], (await _repository.GetCourses()).Select(x => x.Name));
        Assert.Equal(["1-Single", "4-Group"], (await _repository.GetGroupSizes()).Select(x => x.Name));
        Assert.Equal(2, (await _repository.GetAcademicYears()).Count);
    }

    [Fact]
    public async Task GetStudentStatuses_SameRate_OrdersById()
    {
        AddAndSave(new StudentStatus { Id = 4, StudentStatusName = "D", Rate = 200 },
            new StudentStatus { Id = 3, StudentStatusName = "C", Rate = 200 });

        Assert.Equal([2, 3, 4, 1], (await _repository.GetStudentStatuses()).Select(x => x.Id));
    }

    // namesakes are ordered by first name, then by id
    [Fact]
    public async Task SearchHumans_OrdersByLastNameFirstNameAndId()
    {
        AddAndSave(Human(21, "Zeta", "Bo", "05000000021"), Human(22, "Zeta", "Al", "05000000022"),
            Human(20, "Zeta", "Bo", "05000000020"));

        List<LookupItemResponse> result = await _repository.SearchHumans("Zeta", 20);

        Assert.Equal([22, 20, 21], result.Select(x => x.Id));
    }

    [Theory]
    [InlineData("Alpha", new[] { 1 })]
    [InlineData("Ann Alpha", new[] { 1 })]
    [InlineData("Alpha Ann", new[] { 1 })]
    [InlineData("0200", new[] { 4, 3 })]
    [InlineData("000000003", new int[0])]
    public async Task SearchHumans_ByNameOrPersonalIdPrefix(string search, int[] expected)
    {
        List<LookupItemResponse> result = await _repository.SearchHumans(search, 20);

        Assert.Equal(expected, result.Select(x => x.Id));
    }

    [Fact]
    public async Task SearchHumans_ReturnsFullNameAndRespectsLimit()
    {
        List<LookupItemResponse> result = await _repository.SearchHumans("a", 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha Ann", result[0].Name);
    }

    [Fact]
    public async Task AddAndRemove_ChangeTheContext()
    {
        var added = new StudentContract
        {
            ScId = 50,
            ContractNumber = "6.050",
            AcademicYearId = 11,
            StudentHumanId = 1,
            PayerHumanId = 1,
            ContractDate = new DateTime(2026, 9, 29, 0, 0, 0, DateTimeKind.Unspecified)
        };
        _repository.Add(added);
        Assert.Equal(EntityState.Added, _context.Entry(added).State);

        StudentContract? existing = await _repository.GetForChange(3);
        _repository.Remove(existing!);
        Assert.Equal(EntityState.Deleted, _context.Entry(existing!).State);
    }
}
