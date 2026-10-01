using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class CrmCallsRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly CrmCallsRepository _repository;

    public CrmCallsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new CrmCallsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //contracts 10 (Alpha Ann 6.001) and 11 (Beta Bob 6.002) of 2026-2027, 12 (Gamma Gia 6.001) of 2025-2026; call
    //types 1 Reminder, 2 Another; results 1 Off, 2 No answer, 3 Answered. Calls: 1 (10, 03.09 10:00, Answered, must pay
    //10.09), 2 (11, 03.09 10:00, No answer), 3 (10, Another, 15.09 18:00, Off, must pay 20.09), 4 (12, 03.09 10:00,
    //Answered, must pay 12.09), 5 (11, 30.09 09:00, Answered). Synthetic people only: never real names
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
            StudentContract(12, "6.001", 3, 10));
        _context.CrmCallTypes.AddRange(new CrmCallType { CctId = 1, CallTypeName = "Reminder" },
            new CrmCallType { CctId = 2, CallTypeName = "Another" });
        _context.CrmAnswerTypes.AddRange(new CrmAnswerType { CatId = 1, AnswerTypeName = "Off" },
            new CrmAnswerType { CatId = 2, AnswerTypeName = "No answer" },
            new CrmAnswerType { CatId = 3, AnswerTypeName = "Answered" });
        _context.CrmCalls.AddRange(Call(1, 10, 1, At(9, 3, 10), 3, "c1", At(9, 10)),
            Call(2, 11, 1, At(9, 3, 10), 2, null, null), Call(3, 10, 2, At(9, 15, 18), 1, "c3", At(9, 20)),
            Call(4, 12, 1, At(9, 3, 10), 3, null, At(9, 12)), Call(5, 11, 1, At(9, 30, 9), 3, "c5", null));
        _context.SaveChanges();
    }

    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
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

    private static CrmCall Call(int id, int studentContractId, int callTypeId, DateTime callDate, int answerTypeId,
        string? callConversation, DateTime? mustPayDate)
    {
        return new CrmCall
        {
            CcId = id,
            StudentContractId = studentContractId,
            CallTypeId = callTypeId,
            CallDate = callDate,
            AnswerTypeId = answerTypeId,
            CallConversation = callConversation,
            MustPayDate = mustPayDate
        };
    }

    private static CrmCallsListQuery Query(int? studentContractId = null, DateTime? dateFrom = null,
        DateTime? dateTo = null, int? callTypeId = null, int? answerTypeId = null,
        IReadOnlyList<CrmCallSortField>? sortFields = null, int offset = 0, int rowsCount = 100)
    {
        return new CrmCallsListQuery(offset, rowsCount, studentContractId, dateFrom, dateTo, callTypeId, answerTypeId,
            sortFields ?? CrmCallsListQueryFactory.DefaultSortFields);
    }

    private async Task AssertIds(CrmCallsListQuery query, params int[] expected)
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(query);
        Assert.Equal(expected, data.Rows.Select(r => r.Id));
    }

    // Access order: date, "must pay by" descending (an empty one last), student
    [Fact]
    public async Task GetRowsData_ListsAllCallsInTheAccessOrder()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(0, data.Offset);
        Assert.Equal([4, 1, 2, 3, 5], data.Rows.Select(r => r.Id));
    }

    // the student is "last name first name / contract number", as in the Access combo box
    [Fact]
    public async Task GetRowsData_RowHasStudentTypeResultAndTexts()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(
            new CrmCallRowResponse(3, 10, "Alpha Ann / 6.001", At(9, 15, 18), 2, "Another", 1, "Off", "c3",
                At(9, 20)), data.Rows.Single(r => r.Id == 3));
        Assert.Equal(
            new CrmCallRowResponse(2, 11, "Beta Bob / 6.002", At(9, 3, 10), 1, "Reminder", 2, "No answer", null,
                null), data.Rows.Single(r => r.Id == 2));
    }

    [Fact]
    public async Task GetRowsData_FiltersByContract()
    {
        await AssertIds(Query(10), 1, 3);
    }

    // both ends of the range are whole days: the 18:00 call of 15.09 is in "until 15.09"
    [Fact]
    public async Task GetRowsData_FiltersByDaysIncludingBothEnds()
    {
        await AssertIds(Query(dateFrom: At(9, 3), dateTo: At(9, 15)), 4, 1, 2, 3);
        await AssertIds(Query(dateFrom: At(9, 15)), 3, 5);
        await AssertIds(Query(dateTo: At(9, 3)), 4, 1, 2);
    }

    // "until 02.09" ends before the calls of 03.09
    [Fact]
    public async Task GetRowsData_DateTo_ExcludesTheNextDay()
    {
        await AssertIds(Query(dateTo: At(9, 2)));
    }

    [Fact]
    public async Task GetRowsData_FiltersByTypeAndResult()
    {
        await AssertIds(Query(callTypeId: 2), 3);
        await AssertIds(Query(answerTypeId: 3), 4, 1, 5);
    }

    [Fact]
    public async Task GetRowsData_CombinesTheFilters()
    {
        await AssertIds(Query(11, At(9, 1), At(9, 30), 1, 3), 5);
    }

    [Fact]
    public async Task GetRowsData_ReturnsThePage()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(2, data.Offset);
        Assert.Equal([2, 3], data.Rows.Select(r => r.Id));
    }

    // a page that no longer exists after a filter change shows the last page
    [Fact]
    public async Task GetRowsData_OffsetBeyondTheRows_ShowsTheLastPage()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 10, rowsCount: 2));

        Assert.Equal(4, data.Offset);
        Assert.Equal([5], data.Rows.Select(r => r.Id));
    }

    // an offset right at the end is no page either
    [Fact]
    public async Task GetRowsData_OffsetAtTheEnd_ShowsTheLastPage()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 5, rowsCount: 5));

        Assert.Equal(0, data.Offset);
        Assert.Equal([4, 1, 2, 3, 5], data.Rows.Select(r => r.Id));
    }

    // with no rows there is no last page to move to
    [Fact]
    public async Task GetRowsData_NothingFound_KeepsTheOffset()
    {
        CrmCallsRowsDataResponse data = await _repository.GetRowsData(Query(99, offset: 10, rowsCount: 5));

        Assert.Equal(0, data.AllRowsCount);
        Assert.Equal(10, data.Offset);
        Assert.Empty(data.Rows);
    }

    // a second sort field breaks the ties of the first, in its own direction
    [Fact]
    public async Task GetRowsData_SortsBySeveralFields()
    {
        await AssertIds(Query(sortFields:
        [
            new CrmCallSortField(ECrmCallSortField.CallDate, true),
            new CrmCallSortField(ECrmCallSortField.StudentName, false)
        ]), 4, 2, 1, 3, 5);
    }

    [Fact]
    public async Task GetRowsData_UnknownSortField_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetRowsData(Query(sortFields: [new CrmCallSortField((ECrmCallSortField)99, true)])));

        Assert.Equal("sortFields", exception.ParamName);
        Assert.Contains("უცნობი დალაგების ველი", exception.Message, StringComparison.Ordinal);
    }

    // ties are broken by ID; an empty "must pay by" comes first
    [Theory]
    [InlineData(ECrmCallSortField.CallDate, false, new[] { 5, 3, 1, 2, 4 })]
    [InlineData(ECrmCallSortField.StudentName, true, new[] { 1, 3, 2, 5, 4 })]
    [InlineData(ECrmCallSortField.CallTypeName, true, new[] { 3, 1, 2, 4, 5 })]
    [InlineData(ECrmCallSortField.AnswerTypeName, true, new[] { 1, 4, 5, 2, 3 })]
    [InlineData(ECrmCallSortField.MustPayDate, true, new[] { 2, 5, 1, 4, 3 })]
    public async Task GetRowsData_SortsByTheField(ECrmCallSortField field, bool ascending, int[] expected)
    {
        await AssertIds(Query(sortFields: [new CrmCallSortField(field, ascending)]), expected);
    }

    [Fact]
    public async Task GetOne_ReturnsTheCallWithItsContractYear()
    {
        Assert.Equal(new CrmCallResponse(3, 10, "Alpha Ann / 6.001", 11, 2, At(9, 15, 18), 1, "c3", At(9, 20)),
            await _repository.GetOne(3));
        Assert.Equal(new CrmCallResponse(4, 12, "Gamma Gia / 6.001", 10, 1, At(9, 3, 10), 3, null, At(9, 12)),
            await _repository.GetOne(4));
    }

    [Fact]
    public async Task GetOne_MissingCall_IsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_ReturnsTheTrackedCall()
    {
        CrmCall? crmCall = await _repository.GetForChange(2);

        Assert.NotNull(crmCall);
        Assert.Equal(11, crmCall.StudentContractId);
        Assert.Equal(EntityState.Unchanged, _context.Entry(crmCall).State);
        Assert.Null(await _repository.GetForChange(99));
    }

    [Fact]
    public async Task Exists_FindsTheContractTypeAndResult()
    {
        Assert.True(await _repository.StudentContractExists(12));
        Assert.False(await _repository.StudentContractExists(99));
        Assert.True(await _repository.CallTypeExists(2));
        Assert.False(await _repository.CallTypeExists(3));
        Assert.True(await _repository.AnswerTypeExists(3));
        Assert.False(await _repository.AnswerTypeExists(4));
    }

    [Fact]
    public async Task GetCallTypesAndAnswerTypes_AreSortedByName()
    {
        Assert.Equal([new LookupItemResponse(2, "Another"), new LookupItemResponse(1, "Reminder")],
            await _repository.GetCallTypes());
        Assert.Equal([
            new LookupItemResponse(3, "Answered"), new LookupItemResponse(2, "No answer"),
            new LookupItemResponse(1, "Off")
        ], await _repository.GetAnswerTypes());
    }

    [Fact]
    public async Task GetStudentContracts_ListsTheYearsContractsByName()
    {
        Assert.Equal([new LookupItemResponse(10, "Alpha Ann / 6.001"), new LookupItemResponse(11, "Beta Bob / 6.002")],
            await _repository.GetStudentContracts(11));
        Assert.Equal([new LookupItemResponse(12, "Gamma Gia / 6.001")], await _repository.GetStudentContracts(10));
        Assert.Empty(await _repository.GetStudentContracts(99));
    }

    // equal names (a number repeated by mistake) keep the ID order, so the list is stable
    [Fact]
    public async Task GetStudentContracts_WithEqualNames_AreSortedById()
    {
        AddAndSave(StudentContract(14, "6.001", 1, 11));

        List<LookupItemResponse> contracts = await _repository.GetStudentContracts(11);

        Assert.Equal([10, 14], contracts.Where(c => c.Name == "Alpha Ann / 6.001").Select(c => c.Id));
    }

    [Fact]
    public void Add_StoresTheCallOnSave()
    {
        _repository.Add(Call(0, 12, 1, At(10, 1, 11), 2, "new", null));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        Assert.Equal("new", _context.CrmCalls.Single(c => c.StudentContractId == 12 && c.AnswerTypeId == 2)
            .CallConversation);
    }

    [Fact]
    public void Remove_DeletesTheCallOnSave()
    {
        _repository.Remove(_context.CrmCalls.Single(c => c.CcId == 2));
        _context.SaveChanges();

        Assert.False(_context.CrmCalls.Any(c => c.CcId == 2));
        Assert.Equal(4, _context.CrmCalls.Count());
    }
}
