using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class WorkHoursRepositoryTests : IDisposable
{
    private static readonly DateTime ZeroDay = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly MimosiGeDbContext _context;
    private readonly WorkHoursRepository _repository;

    public WorkHoursRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new WorkHoursRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Date(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static DateTime At(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, DateTimeKind.Unspecified);

    //employees (contracts with a work hour group): 1 (Alpha Ann T3.01, 12:00-18:00), 5 (Beta Bob T3.05, 18:00-20:00,
    //ends 31.12), 15 (Alpha Ann T3.10, no fixed hours); 20 (Gamma Gia T1.01) is a teacher without a group. Records:
    //1 (1, 15.09 11:55-18:05), 2 (5, 15.09 17:55-20:00), 3 (1, 16.09 11:58-18:00), 4 (15, 30.09 09:00, not ended),
    //5 (1, 31.08 23:00 - 01.09 01:00). Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(Human(1, "Alpha", "Ann"), Human(2, "Beta", "Bob"), Human(3, "Gamma", "Gia"));
        _context.WorkHourGroups.AddRange(new WorkHourGroup { WhgId = 1, WhgKey = "administration", WhgName = "A" },
            new WorkHourGroup { WhgId = 2, WhgKey = "Media Manager", WhgName = "M" });
        _context.TeacherContracts.AddRange(Contract(1, "T3.01", 1, 1, 12, 18, Date(2020, 9, 1), null),
            Contract(5, "T3.05", 2, 1, 18, 20, Date(2020, 9, 16), At(12, 31)),
            Contract(15, "T3.10", 1, 2, null, null, Date(2023, 9, 19), null),
            Contract(20, "T1.01", 3, null, null, null, Date(2023, 9, 19), null));
        _context.WorkHours.AddRange(Record(1, 1, At(9, 15, 11, 55), At(9, 15, 18, 5)),
            Record(2, 5, At(9, 15, 17, 55), At(9, 15, 20)), Record(3, 1, At(9, 16, 11, 58), At(9, 16, 18)),
            Record(4, 15, At(9, 30, 9), null), Record(5, 1, At(8, 31, 23), At(9, 1, 1)));
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

    private static TeacherContract Contract(int id, string number, int humanId, int? workHourGroupId, int? startHour,
        int? endHour, DateTime contractDate, DateTime? contractEndDate)
    {
        return new TeacherContract
        {
            Id = id,
            ContractNumber = number,
            ContractDate = contractDate,
            ContractEndDate = contractEndDate,
            TeacherHumanId = humanId,
            RsCountryId = 1,
            WorkHourGroupId = workHourGroupId,
            WorkHoursStart = startHour is null ? null : ZeroDay.AddHours(startHour.Value),
            WorkHoursEnd = endHour is null ? null : ZeroDay.AddHours(endHour.Value)
        };
    }

    private static WorkHour Record(int id, int teacherContractId, DateTime start, DateTime? end)
    {
        return new WorkHour { WhId = id, TeacherContractId = teacherContractId, WhStart = start, WhEnd = end };
    }

    private static Lesson Lesson(int id, DateTime lessonDt, int statusId = 1)
    {
        return new Lesson
        {
            Id = id,
            GroupId = 1,
            TeacherContractId = 20,
            LessonDt = lessonDt,
            SalarySchemaId = 1,
            LessonStatusId = statusId,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
    }

    private static LessonByStudent StudentRow(int id, int lessonId, float hoursCount)
    {
        return new LessonByStudent { Id = id, LessonId = lessonId, StudentContractId = 1, HoursCount = hoursCount };
    }

    private static WorkHoursListQuery Query(int? teacherContractId = null, DateTime? dateFrom = null,
        DateTime? dateTo = null, IReadOnlyList<WorkHourSortField>? sortFields = null, int offset = 0,
        int rowsCount = 100)
    {
        return new WorkHoursListQuery(offset, rowsCount, teacherContractId, dateFrom, dateTo,
            sortFields ?? WorkHoursListQueryFactory.DefaultSortFields);
    }

    private async Task AssertIds(WorkHoursListQuery query, params int[] expected)
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(query);
        Assert.Equal(expected, data.Rows.Select(r => r.Id));
    }

    // Access order: the start
    [Fact]
    public async Task GetRowsData_ListsAllRecordsByStart()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(0, data.Offset);
        Assert.Equal([5, 1, 2, 3, 4], data.Rows.Select(r => r.Id));
    }

    // the employee is "last name first name / contract number", as in the Access combo box; the duration in hours
    [Fact]
    public async Task GetRowsData_RowHasEmployeeTimesAndDuration()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(new WorkHourRowResponse(1, 1, "Alpha Ann / T3.01", At(9, 15, 11, 55), At(9, 15, 18, 5), 6.17m),
            data.Rows.Single(r => r.Id == 1));
        Assert.Equal(new WorkHourRowResponse(4, 15, "Alpha Ann / T3.10", At(9, 30, 9), null, null),
            data.Rows.Single(r => r.Id == 4));
    }

    // per employee over every record of the filter (all pages): finished hours and all records, sorted by name
    [Fact]
    public async Task GetRowsData_TotalsEveryRecordOfTheFilter()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query(rowsCount: 1));

        Assert.Single(data.Rows);
        Assert.Equal([
            new WorkHoursTotalResponse(1, "Alpha Ann / T3.01", 14.2m, 3),
            new WorkHoursTotalResponse(15, "Alpha Ann / T3.10", 0m, 1),
            new WorkHoursTotalResponse(5, "Beta Bob / T3.05", 2.08m, 1)
        ], data.Totals);
    }

    [Fact]
    public async Task GetRowsData_TotalsFollowTheFilter()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query(1, At(9, 15), At(9, 15)));

        Assert.Equal([new WorkHoursTotalResponse(1, "Alpha Ann / T3.01", 6.17m, 1)], data.Totals);
    }

    [Fact]
    public async Task GetRowsData_FiltersByEmployee()
    {
        await AssertIds(Query(1), 5, 1, 3);
    }

    // Access: the end is on or after "from" and the start on or before "until" (to the end of that day)
    [Fact]
    public async Task GetRowsData_FiltersByDays()
    {
        await AssertIds(Query(dateFrom: At(9, 16)), 3, 4);
        await AssertIds(Query(dateTo: At(9, 15)), 5, 1, 2);
        await AssertIds(Query(dateFrom: At(9, 15), dateTo: At(9, 16)), 1, 2, 3);
    }

    // a record over midnight belongs to both days: it started on 31.08 and ended on 01.09
    [Fact]
    public async Task GetRowsData_RecordOverMidnight_IsInBothDays()
    {
        await AssertIds(Query(dateFrom: At(9, 1), dateTo: At(9, 1)), 5);
        await AssertIds(Query(dateFrom: At(8, 31), dateTo: At(8, 31)), 5);
        await AssertIds(Query(dateTo: At(8, 30)));
    }

    // unlike Access, a record not ended yet is filtered by its start, so a work started today is in the list
    [Fact]
    public async Task GetRowsData_RecordNotEnded_IsFilteredByItsStart()
    {
        await AssertIds(Query(dateFrom: At(9, 30), dateTo: At(9, 30)), 4);
        await AssertIds(Query(dateFrom: At(10, 1)));
    }

    // a record starting exactly at midnight belongs to its own day: "until 15.09" ends before it
    [Fact]
    public async Task GetRowsData_RecordStartingAtMidnight_BelongsToItsDay()
    {
        AddAndSave(Record(6, 5, At(9, 16), At(9, 16, 1)));

        await AssertIds(Query(5, dateTo: At(9, 15)), 2);
        await AssertIds(Query(5, At(9, 16), At(9, 16)), 6);
    }

    [Fact]
    public async Task GetRowsData_CombinesTheFilters()
    {
        await AssertIds(Query(1, At(9, 2), At(9, 30)), 1, 3);
    }

    [Fact]
    public async Task GetRowsData_ReturnsThePage()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(2, data.Offset);
        Assert.Equal([2, 3], data.Rows.Select(r => r.Id));
    }

    // a page that the filter no longer reaches shows the last page
    [Theory]
    [InlineData(5, 4)]
    [InlineData(40, 4)]
    public async Task GetRowsData_PageBeyondTheEnd_ShowsTheLastPage(int offset, int expectedOffset)
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query(offset: offset, rowsCount: 2));

        Assert.Equal(expectedOffset, data.Offset);
        Assert.Equal([4], data.Rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRowsData_NothingFound_IsAnEmptyFirstPage()
    {
        WorkHoursRowsDataResponse data = await _repository.GetRowsData(Query(20, offset: 10));

        Assert.Equal(0, data.AllRowsCount);
        Assert.Equal(10, data.Offset);
        Assert.Empty(data.Rows);
        Assert.Empty(data.Totals);
    }

    // a record without an end sorts first (as in SQL Server)
    [Theory]
    [InlineData(EWorkHourSortField.WhEnd, true, 4, 5, 1, 2, 3)]
    [InlineData(EWorkHourSortField.WhEnd, false, 3, 2, 1, 5, 4)]
    [InlineData(EWorkHourSortField.WhStart, false, 4, 3, 2, 1, 5)]
    [InlineData(EWorkHourSortField.EmployeeName, false, 2, 4, 1, 3, 5)]
    public async Task GetRowsData_SortsByTheField(EWorkHourSortField field, bool ascending, params int[] expected)
    {
        await AssertIds(Query(sortFields: [new WorkHourSortField(field, ascending)]), expected);
    }

    // equal values keep a stable page order by id
    [Fact]
    public async Task GetRowsData_SameName_IsSortedById()
    {
        await AssertIds(Query(sortFields: [new WorkHourSortField(EWorkHourSortField.EmployeeName, true)]), 1, 3, 5, 4,
            2);
    }

    // a second sort field breaks the ties of the first, in its own direction
    [Fact]
    public async Task GetRowsData_SortsBySeveralFields()
    {
        await AssertIds(Query(sortFields:
        [
            new WorkHourSortField(EWorkHourSortField.EmployeeName, true),
            new WorkHourSortField(EWorkHourSortField.WhStart, false)
        ]), 3, 1, 5, 4, 2);
    }

    [Fact]
    public async Task GetRowsData_UnknownSortField_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetRowsData(Query(sortFields: [new WorkHourSortField((EWorkHourSortField)99, true)])));

        Assert.Equal("sortFields", exception.ParamName);
        Assert.Contains("უცნობი დალაგების ველი", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOne_ReturnsTheRecordWithItsEmployee()
    {
        Assert.Equal(new WorkHourResponse(4, 15, "Alpha Ann / T3.10", At(9, 30, 9), null), await _repository.GetOne(4));
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_ReturnsATrackedRecord()
    {
        WorkHour? workHour = await _repository.GetForChange(2);

        Assert.NotNull(workHour);
        Assert.Equal(EntityState.Unchanged, _context.Entry(workHour).State);
        Assert.Null(await _repository.GetForChange(99));
    }

    // an employee is a contract with a work hour group
    [Theory]
    [InlineData(1, true)]
    [InlineData(15, true)]
    [InlineData(20, false)]
    [InlineData(99, false)]
    public async Task EmployeeExists_OnlyForContractsWithAWorkHourGroup(int teacherContractId, bool expected)
    {
        Assert.Equal(expected, await _repository.EmployeeExists(teacherContractId));
    }

    [Fact]
    public async Task GetEmployee_ReturnsTheContractDatesAndFixedHours()
    {
        Assert.Equal(
            new WorkHourEmployee(5, "Beta Bob / T3.05", Date(2020, 9, 16), At(12, 31), ZeroDay.AddHours(18),
                ZeroDay.AddHours(20)), await _repository.GetEmployee(5));
        Assert.Null(await _repository.GetEmployee(20));
    }

    [Fact]
    public async Task GetEmployees_ReturnsEveryEmployee()
    {
        List<WorkHourEmployee> employees = await _repository.GetEmployees();

        Assert.Equal([1, 5, 15], employees.Select(e => e.Id).Order());
        Assert.Equal(new WorkHourEmployee(15, "Alpha Ann / T3.10", Date(2023, 9, 19), null, null, null),
            employees.Single(e => e.Id == 15));
    }

    // the Access combo box: employees only, by "last name first name / contract number"
    [Fact]
    public async Task GetEmployeeLookups_ListsTheEmployeesByName()
    {
        Assert.Equal([
            new LookupItemResponse(1, "Alpha Ann / T3.01"), new LookupItemResponse(15, "Alpha Ann / T3.10"),
            new LookupItemResponse(5, "Beta Bob / T3.05")
        ], await _repository.GetEmployeeLookups());
    }

    // a record belongs to the day it started on
    [Theory]
    [InlineData(1, 9, 15, true)]
    [InlineData(1, 9, 17, false)]
    [InlineData(5, 9, 16, false)]
    [InlineData(1, 8, 31, true)]
    [InlineData(1, 9, 1, false)]
    public async Task HasRecordOnDay_ChecksTheStartsDay(int teacherContractId, int month, int day, bool expected)
    {
        Assert.Equal(expected, await _repository.HasRecordOnDay(teacherContractId, At(month, day, 15)));
    }

    // a record starting exactly at midnight is that day's, not the day before's
    [Fact]
    public async Task HasRecordOnDay_RecordAtMidnight_IsThatDays()
    {
        AddAndSave(Record(6, 5, At(9, 17), null));

        Assert.True(await _repository.HasRecordOnDay(5, At(9, 17, 12)));
        Assert.False(await _repository.HasRecordOnDay(5, At(9, 16, 12)));
    }

    // several records on one day: the latest started one, on equal starts the latest added
    [Fact]
    public async Task GetLastRecordOfDayForChange_ReturnsTheLatestStartedRecord()
    {
        AddAndSave(Record(6, 1, At(9, 15, 19), null), Record(7, 1, At(9, 15, 8), null),
            Record(8, 5, At(9, 15, 17, 55), null));

        WorkHour? ofContract1 = await _repository.GetLastRecordOfDayForChange(1, At(9, 15, 12));
        WorkHour? ofContract5 = await _repository.GetLastRecordOfDayForChange(5, At(9, 15));

        Assert.Equal(6, ofContract1?.WhId);
        Assert.Equal(8, ofContract5?.WhId);
        Assert.Equal(EntityState.Unchanged, _context.Entry(ofContract1!).State);
        Assert.Null(await _repository.GetLastRecordOfDayForChange(1, At(9, 17)));
    }

    [Fact]
    public async Task GetLastRecordOfDayForChange_RecordAtMidnight_IsThatDays()
    {
        AddAndSave(Record(6, 5, At(9, 17), null));

        Assert.Equal(6, (await _repository.GetLastRecordOfDayForChange(5, At(9, 17, 12)))?.WhId);
        Assert.Null(await _repository.GetLastRecordOfDayForChange(5, At(9, 16, 12)));
    }

    // every student row of the lessons in the range, except the cancelled ones (status 2) and lessons without students
    [Fact]
    public async Task GetLessonTimes_ReturnsTheStudentRowsOfTheLessonsInTheRange()
    {
        AddAndSave(Lesson(1, At(9, 15, 10)), Lesson(2, At(9, 15, 18), 2), Lesson(3, At(9, 16, 9)),
            Lesson(4, At(9, 17, 10), 3), Lesson(5, At(9, 14, 23)), Lesson(6, At(10, 1)), Lesson(7, At(9, 15)),
            StudentRow(1, 1, 1.5f), StudentRow(2, 1, 2f), StudentRow(3, 2, 1f), StudentRow(4, 4, 1f),
            StudentRow(5, 5, 1f), StudentRow(6, 6, 1f), StudentRow(7, 7, 1f));

        List<LessonTimeData> times = await _repository.GetLessonTimes(At(9, 15), At(10, 1));

        Assert.Equal([
            new LessonTimeData(At(9, 15), 1f), new LessonTimeData(At(9, 15, 10), 1.5f),
            new LessonTimeData(At(9, 15, 10), 2f), new LessonTimeData(At(9, 17, 10), 1f)
        ], times.OrderBy(t => t.LessonDt).ThenBy(t => t.HoursCount));
    }

    // the days of the records started in the range, without their times
    [Fact]
    public async Task GetRecordDays_ReturnsTheStartDaysInTheRange()
    {
        AddAndSave(Record(6, 15, At(9, 15), At(9, 15, 1)), Record(7, 15, At(9, 17), null));

        List<WorkHourDay> days = await _repository.GetRecordDays(At(9, 15), At(9, 17));

        Assert.Equal([
            new WorkHourDay(1, At(9, 15)), new WorkHourDay(1, At(9, 16)), new WorkHourDay(5, At(9, 15)),
            new WorkHourDay(15, At(9, 15))
        ], days.OrderBy(d => d.TeacherContractId).ThenBy(d => d.Day));
    }

    [Fact]
    public void Add_StoresTheRecordOnSave()
    {
        _repository.Add(Record(0, 5, At(9, 17, 18), At(9, 17, 20)));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        Assert.Equal(At(9, 17, 20), _context.WorkHours.Single(w => w.WhStart == At(9, 17, 18)).WhEnd);
    }

    [Fact]
    public void Remove_DeletesTheRecordOnSave()
    {
        _repository.Remove(_context.WorkHours.Single(w => w.WhId == 2));
        _context.SaveChanges();

        Assert.False(_context.WorkHours.Any(w => w.WhId == 2));
        Assert.Equal(4, _context.WorkHours.Count());
    }
}
