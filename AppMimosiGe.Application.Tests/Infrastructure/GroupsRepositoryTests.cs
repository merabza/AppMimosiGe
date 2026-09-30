using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class GroupsRepositoryTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly MimosiGeDbContext _context;
    private readonly GroupsRepository _repository;

    public GroupsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new GroupsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Date(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //groups 1 ("1001", this year, active), 2 ("201", this year, voided today) and 3 ("1001", last year, voided in
    //10 days). Synthetic people only: never real names
    private void Seed()
    {
        _context.AcademicYears.AddRange(
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = new DateTime(2025, 9, 1, 0, 0, 0,
                    DateTimeKind.Unspecified),
                FinishDate = Date(9, 1)
            },
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = Date(9, 1),
                FinishDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
            });
        _context.Courses.AddRange(new Course { CrsId = 1, CourseName = "Math" },
            new Course { CrsId = 2, CourseName = "English" });
        _context.GroupSizes.AddRange(new GroupSize { GrsId = 1, GrsSize = 2, GrsName = "Pair" },
            new GroupSize { GrsId = 2, GrsSize = 4, GrsName = "Four" },
            new GroupSize { GrsId = 3, GrsSize = 1, GrsName = "Solo" });
        _context.StudentStatuses.AddRange(new StudentStatus { Id = 1, StudentStatusName = "Tenth", Rate = 110 },
            new StudentStatus { Id = 2, StudentStatusName = "Ninth", Rate = 109 });
        _context.Humans.AddRange(Human(1, "Alpha", "Ann"), Human(2, "Beta", "Bob"), Human(3, "Gamma", "Gia"),
            Human(4, "Delta", "Dan"), Human(5, "Epsilon", "Eva"));
        _context.TeacherSalarySchemes.AddRange(new TeacherSalaryScheme { Id = 1, SchemaName = "Senior" },
            new TeacherSalaryScheme { Id = 2, SchemaName = "Junior" });
        _context.RsCountries.Add(new RsCountry { Id = 1, Code = "GE", CountryName = "Georgia" });
        _context.TeacherContracts.AddRange(
            new TeacherContract
            {
                Id = 1, ContractNumber = "T3.01", TeacherHumanId = 1, RsCountryId = 1, SalarySchemaByHoursId = 1
            }, new TeacherContract { Id = 2, ContractNumber = "T3.02", TeacherHumanId = 2, RsCountryId = 1 });
        _context.StudentContracts.AddRange(StudentContract(10, "6.001", 3, 11), StudentContract(11, "6.002", 4, 11),
            StudentContract(12, "5.001", 5, 10));
        _context.StudentContractDetails.AddRange(
            new StudentContractDetail
            {
                Id = 2, StudentContractId = 10, CourseId = 2, GroupSizeId = 3, FourWeekHours = 8, FourWeekFee = 80,
                OneHourFee = 10
            },
            new StudentContractDetail
            {
                Id = 1, StudentContractId = 10, CourseId = 1, GroupSizeId = 2, FourWeekHours = 12, FourWeekFee = 72,
                OneHourFee = 6
            });
        _context.WeekDays.AddRange(new WeekDay { Id = 1, Name = "Monday", ShortName = "1-Mo", WeekDayNumber = 1 },
            new WeekDay { Id = 3, Name = "Wednesday", ShortName = "3-We", WeekDayNumber = 3 });
        _context.LessonStartTimes.AddRange(new LessonStartTime { LstId = 2, LstTime = new TimeOnly(8, 30) },
            new LessonStartTime { LstId = 1, LstTime = new TimeOnly(8, 0) });
        _context.Rooms.AddRange(new Room { Id = 1, RoomName = "B" }, new Room { Id = 2, RoomName = "A" });
        _context.Groups.AddRange(Group(1, "1001", 11, 1, 2, 1, null, false), Group(2, "201", 11, 2, 3, 2, Today, true),
            Group(3, "1001", 10, 1, 1, 1, Today.AddDays(10), false));
        _context.GroupsByTeachers.AddRange(Teacher(11, 1, 1, Date(9, 1), Date(9, 20)),
            Teacher(12, 1, 2, Date(9, 20), null), Teacher(13, 2, 1, Date(9, 1), null),
            Teacher(14, 3, 2, Date(10, 5), null));
        _context.GroupsByStudents.AddRange(Student(21, 1, 10, Date(9, 1), null),
            Student(22, 1, 11, Date(9, 1), Today), Student(23, 1, 11, Date(10, 1), null),
            Student(24, 2, 10, Date(9, 1), null),
            Student(25, 3, 12, new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), null));
        _context.GroupDayTimePlaces.AddRange(
            new GroupDayTimePlace
            {
                GdtpId = 31, GroupId = 1, WeekDayId = 3, LessonStartTimeId = 2, HoursCount = 1.5f, RoomId = 1,
                StartDate = Date(9, 1)
            },
            new GroupDayTimePlace
            {
                GdtpId = 32, GroupId = 1, WeekDayId = 1, LessonStartTimeId = 1, HoursCount = 2, RoomId = 2,
                StartDate = Date(8, 1), EndDate = Date(9, 1)
            });
        _context.SaveChanges();
    }

    //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
    //so test data is saved synchronously
    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human
        {
            HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}"
        };
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

    private static Group Group(int id, string code, int academicYearId, int courseId, int groupSizeId,
        int studentStatusId, DateTime? voidDate, bool dirtyLessons)
    {
        return new Group
        {
            GrpId = id,
            GroupCode = code,
            AcademicYearId = academicYearId,
            CourseId = courseId,
            GroupSizeId = groupSizeId,
            StudentStatusId = studentStatusId,
            VoidDate = voidDate,
            DirtyLessons = dirtyLessons
        };
    }

    private static GroupByTeacher Teacher(int id, int groupId, int teacherContractId, DateTime startDate,
        DateTime? endDate)
    {
        return new GroupByTeacher
        {
            Id = id,
            GroupId = groupId,
            TeacherContractId = teacherContractId,
            SalarySchemaId = teacherContractId,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    private static GroupByStudent Student(int id, int groupId, int studentContractId, DateTime startDate,
        DateTime? endDate)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = groupId,
            StudentContractId = studentContractId,
            StartDate = startDate,
            EndDate = endDate,
            Note = id == 21 ? "note" : null
        };
    }

    private static GroupsListQuery Query(EGroupFindMethod findMethod = EGroupFindMethod.Group,
        int? academicYearId = null, EGroupState? state = null, int? courseId = null, int? groupSizeId = null,
        int? studentStatusId = null, string? search = null, IReadOnlyList<GroupSortField>? sort = null,
        int offset = 0, int rowsCount = 10)
    {
        return new GroupsListQuery(offset, rowsCount, Today, findMethod, academicYearId, state, courseId, groupSizeId,
            studentStatusId, search, sort ?? GroupsListQueryFactory.DefaultSortFields(findMethod));
    }

    private static IReadOnlyList<GroupSortField> SortBy(EGroupSortField field, bool ascending = true)
    {
        return [new GroupSortField(field, ascending)];
    }

    // the teacher and the students of today: [StartDate, EndDate) contains today
    [Fact]
    public async Task GetRowsData_GroupMode_ListsGroupsByCodeWithTodaysTeacherAndStudents()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query());

        Assert.Equal(3, result.AllRowsCount);
        Assert.Equal([1, 3, 2], result.Rows.Select(r => r.RowId));
        Assert.Equal(
            new GroupRowResponse(1, 1, "1001", "2026-2027", "Math", "4-Four", "Tenth", null, false, "Beta Bob", 1,
                null, null, null), result.Rows[0]);
        Assert.Null(result.Rows[1].TeacherName);
        Assert.Equal(1, result.Rows[1].ActiveStudentsCount);
        Assert.Equal("Alpha Ann", result.Rows[2].TeacherName);
        Assert.Equal("1-Solo", result.Rows[2].GroupSizeName);
        Assert.Equal(Today, result.Rows[2].VoidDate);
        Assert.True(result.Rows[2].DirtyLessons);
    }

    // a group voided today is not active any more; one voided in the future still is
    [Theory]
    [InlineData(EGroupState.Active, new[] { 1, 3 })]
    [InlineData(EGroupState.Voided, new[] { 2 })]
    public async Task GetRowsData_State_FiltersByTheVoidDate(EGroupState state, int[] expectedIds)
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(state: state));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.GrpId));
    }

    [Fact]
    public async Task GetRowsData_FiltersByTheGroupFields()
    {
        Assert.Equal([1, 2], (await _repository.GetRowsData(Query(academicYearId: 11))).Rows.Select(r => r.GrpId));
        Assert.Equal([2], (await _repository.GetRowsData(Query(courseId: 2))).Rows.Select(r => r.GrpId));
        Assert.Equal([3], (await _repository.GetRowsData(Query(groupSizeId: 1))).Rows.Select(r => r.GrpId));
        Assert.Equal([2], (await _repository.GetRowsData(Query(studentStatusId: 2))).Rows.Select(r => r.GrpId));
        Assert.Equal([2], (await _repository.GetRowsData(Query(search: "20"))).Rows.Select(r => r.GrpId));
    }

    [Fact]
    public async Task GetRowsData_TeacherMode_ListsEveryTeacherPeriodByName()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Teacher));

        Assert.Equal(4, result.AllRowsCount);
        Assert.Equal([11, 13, 12, 14], result.Rows.Select(r => r.RowId));
        Assert.Equal(
            new GroupRowResponse(11, 1, "1001", "2026-2027", "Math", "4-Four", "Tenth", null, false, "Alpha Ann", null,
                null, Date(9, 1), Date(9, 20)), result.Rows[0]);
    }

    [Theory]
    [InlineData("Alpha Ann", new[] { 11, 13 })]
    [InlineData("Ann Alpha", new[] { 11, 13 })]
    [InlineData("Bob", new[] { 12, 14 })]
    [InlineData("nobody", new int[0])]
    public async Task GetRowsData_TeacherMode_SearchesTheTeacherName(string search, int[] expectedIds)
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Teacher,
            search: search));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.RowId));
    }

    // the group filters apply to the teacher rows too
    [Fact]
    public async Task GetRowsData_TeacherMode_AppliesTheGroupFilters()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Teacher,
            state: EGroupState.Active));

        Assert.Equal([11, 12, 14], result.Rows.Select(r => r.RowId));
    }

    [Fact]
    public async Task GetRowsData_StudentMode_ListsEveryStudentPeriodByName()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Student));

        Assert.Equal(5, result.AllRowsCount);
        Assert.Equal([22, 23, 25, 21, 24], result.Rows.Select(r => r.RowId));
        Assert.Equal(
            new GroupRowResponse(22, 1, "1001", "2026-2027", "Math", "4-Four", "Tenth", null, false, null, null,
                "Delta Dan", Date(9, 1), Today), result.Rows[0]);
    }

    [Theory]
    [InlineData("Gamma", new[] { 21, 24 })]
    [InlineData("Gia Gamma", new[] { 21, 24 })]
    [InlineData("Alpha", new int[0])]
    public async Task GetRowsData_StudentMode_SearchesTheStudentName(string search, int[] expectedIds)
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Student,
            search: search));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.RowId));
    }

    // the size by its seats and the status by its rate, as in the Access lists
    [Theory]
    [InlineData(EGroupSortField.GroupSizeName, true, new[] { 2, 3, 1 })]
    [InlineData(EGroupSortField.StudentStatusName, true, new[] { 2, 1, 3 })]
    [InlineData(EGroupSortField.TeacherName, true, new[] { 3, 2, 1 })]
    [InlineData(EGroupSortField.ActiveStudentsCount, false, new[] { 1, 2, 3 })]
    [InlineData(EGroupSortField.VoidDate, true, new[] { 1, 2, 3 })]
    [InlineData(EGroupSortField.DirtyLessons, false, new[] { 2, 1, 3 })]
    [InlineData(EGroupSortField.AcademicYearName, true, new[] { 3, 1, 2 })]
    [InlineData(EGroupSortField.CourseName, false, new[] { 1, 3, 2 })]
    [InlineData(EGroupSortField.GroupCode, false, new[] { 2, 1, 3 })]
    public async Task GetRowsData_GroupMode_SortsByTheRequestedField(EGroupSortField field, bool ascending,
        int[] expectedIds)
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(sort: SortBy(field, ascending)));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.RowId));
    }

    [Theory]
    [InlineData(EGroupSortField.EndDate, false, new[] { 22, 21, 23, 24, 25 })]
    [InlineData(EGroupSortField.StartDate, true, new[] { 25, 21, 22, 24, 23 })]
    [InlineData(EGroupSortField.StudentName, false, new[] { 21, 24, 25, 22, 23 })]
    public async Task GetRowsData_StudentMode_SortsByTheRequestedField(EGroupSortField field, bool ascending,
        int[] expectedIds)
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Student,
            sort: SortBy(field, ascending)));

        Assert.Equal(expectedIds, result.Rows.Select(r => r.RowId));
    }

    [Fact]
    public async Task GetRowsData_ReturnsTheRequestedPage()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(EGroupFindMethod.Student, offset: 2,
            rowsCount: 2));

        Assert.Equal(5, result.AllRowsCount);
        Assert.Equal(2, result.Offset);
        Assert.Equal([25, 21], result.Rows.Select(r => r.RowId));
    }

    // after a filter change the page may no longer exist, then the last page is shown
    [Fact]
    public async Task GetRowsData_OffsetBeyondTheEnd_ReturnsTheLastPage()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(offset: 10, rowsCount: 2));

        Assert.Equal(2, result.Offset);
        Assert.Equal([2], result.Rows.Select(r => r.RowId));
    }

    [Fact]
    public async Task GetRowsData_NoMatchingRows_KeepsTheOffset()
    {
        GroupsRowsDataResponse result = await _repository.GetRowsData(Query(search: "nothing", offset: 5));

        Assert.Equal(0, result.AllRowsCount);
        Assert.Equal(5, result.Offset);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task GetRowsData_UnknownSortField_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetRowsData(Query(sort: SortBy((EGroupSortField)99))));

        Assert.Equal("sortFields", ex.ParamName);
    }

    // teachers and the schedule by their start, students by name, start and end (the Access subforms)
    [Fact]
    public async Task GetOne_ReturnsTheGroupWithOrderedRows()
    {
        GroupResponse? result = await _repository.GetOne(1);

        Assert.NotNull(result);
        Assert.Equal((1, 11, "1001", 1, 2, 1, (DateTime?)null, false),
            (result.GrpId, result.AcademicYearId, result.GroupCode, result.CourseId, result.GroupSizeId,
                result.StudentStatusId, result.VoidDate, result.DirtyLessons));
        Assert.Equal([
            new GroupTeacherResponse(11, 1, 1, Date(9, 1), Date(9, 20)),
            new GroupTeacherResponse(12, 2, 2, Date(9, 20), null)
        ], result.Teachers);
        Assert.Equal([22, 23, 21], result.Students.Select(s => s.Id));
        Assert.Equal(new GroupStudentResponse(21, 10, "Gamma Gia / 6.001", 8, 48, 6, 1, Date(9, 1), null, "note"),
            result.Students[2]);
        Assert.Equal("Delta Dan / 6.002", result.Students[0].StudentContractName);
        Assert.Equal([
            new GroupDayTimePlaceResponse(32, 1, 1, 2, 2, Date(8, 1), Date(9, 1)),
            new GroupDayTimePlaceResponse(31, 3, 2, 1.5f, 1, Date(9, 1), null)
        ], result.DayTimePlaces);
    }

    [Fact]
    public async Task GetOne_Missing_ReturnsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetForChange_ReturnsTrackedGroupWithItsRows()
    {
        Group? result = await _repository.GetForChange(1);

        Assert.NotNull(result);
        Assert.Equal(EntityState.Unchanged, _context.Entry(result).State);
        Assert.Equal([11, 12], result.GroupsByTeachers.Select(t => t.Id).Order());
        Assert.Equal([21, 22, 23], result.GroupsByStudents.Select(s => s.GbsId).Order());
        Assert.Equal([31, 32], result.GroupDayTimePlaces.Select(d => d.GdtpId).Order());
        Assert.Null(await _repository.GetForChange(99));
    }

    // the code is unique within the academic year; the edited group is excluded
    [Theory]
    [InlineData(11, "1001", 0, true)]
    [InlineData(11, "1001", 1, false)]
    [InlineData(11, "1001", 2, true)]
    [InlineData(10, "1001", 0, true)]
    [InlineData(10, "201", 0, false)]
    public async Task GroupCodeExists_ChecksTheYearExceptTheEdited(int academicYearId, string code, int exceptId,
        bool expected)
    {
        Assert.Equal(expected, await _repository.GroupCodeExists(academicYearId, code, exceptId));
    }

    [Fact]
    public async Task IsInUse_GroupWithoutLessons_IsFalse()
    {
        Assert.False(await _repository.IsInUse(1));
    }

    [Fact]
    public async Task IsInUse_Lesson_IsTrue()
    {
        AddAndSave(new Lesson { Id = 1, GroupId = 1, TeacherContractId = 1, SalarySchemaId = 1 });

        Assert.True(await _repository.IsInUse(1));
        Assert.False(await _repository.IsInUse(2));
    }

    [Fact]
    public async Task IsInUse_SalaryLineDetail_IsTrue()
    {
        AddAndSave(new SalaryLineDetail { SadId = 1, SaId = 1, GroupId = 2 });

        Assert.True(await _repository.IsInUse(2));
        Assert.False(await _repository.IsInUse(1));
    }

    // a lesson student can point to a student row of the group even when the lesson is another group's
    [Fact]
    public async Task IsInUse_LessonOfAStudentRow_IsTrue()
    {
        AddAndSave(new LessonByStudent { Id = 1, LessonId = 1, StudentContractId = 12, GroupByStudentId = 25 });

        Assert.True(await _repository.IsInUse(3));
        Assert.False(await _repository.IsInUse(1));
    }

    [Fact]
    public async Task AnyStudentRowIsInUse_FindsRowsWithLessons()
    {
        AddAndSave(new LessonByStudent { Id = 1, LessonId = 1, StudentContractId = 11, GroupByStudentId = 22 },
            new LessonByStudent { Id = 2, LessonId = 1, StudentContractId = 10, GroupByStudentId = null });

        Assert.True(await _repository.AnyStudentRowIsInUse([21, 22]));
        Assert.False(await _repository.AnyStudentRowIsInUse([21, 23]));
        Assert.False(await _repository.AnyStudentRowIsInUse([]));
    }

    [Fact]
    public async Task ExistsChecks_FindOnlyExistingRecords()
    {
        Assert.True(await _repository.TeacherContractExists(2));
        Assert.False(await _repository.TeacherContractExists(99));
        Assert.True(await _repository.StudentContractExists(12));
        Assert.False(await _repository.StudentContractExists(99));
        Assert.True(await _repository.WeekDayExists(3));
        Assert.False(await _repository.WeekDayExists(2));
        Assert.True(await _repository.LessonStartTimeExists(2));
        Assert.False(await _repository.LessonStartTimeExists(99));
        Assert.True(await _repository.RoomExists(1));
        Assert.False(await _repository.RoomExists(99));
    }

    [Fact]
    public async Task GetDefaultSalarySchemeId_ReturnsTheContractsScheme()
    {
        Assert.Equal(1, await _repository.GetDefaultSalarySchemeId(1));
        Assert.Null(await _repository.GetDefaultSalarySchemeId(2));
        Assert.Null(await _repository.GetDefaultSalarySchemeId(99));
    }

    [Fact]
    public async Task GetTeacherContracts_AreSortedByNameAndNumberWithTheirDefaultScheme()
    {
        AddAndSave(new TeacherContract { Id = 3, ContractNumber = "T0.01", TeacherHumanId = 2, RsCountryId = 1 });

        Assert.Equal([
            new GroupTeacherContractLookupResponse(1, "Alpha Ann / T3.01", 1),
            new GroupTeacherContractLookupResponse(3, "Beta Bob / T0.01", null),
            new GroupTeacherContractLookupResponse(2, "Beta Bob / T3.02", null)
        ], await _repository.GetTeacherContracts());
    }

    [Fact]
    public async Task GetStudentContracts_ListsTheContractsOfTheYearWithTheirTariffs()
    {
        List<GroupStudentContractLookupResponse> result = await _repository.GetStudentContracts(11);

        Assert.Equal([11, 10], result.Select(c => c.ScId));
        Assert.Equal("Delta Dan / 6.002", result[0].Name);
        Assert.Empty(result[0].Tariffs);
        Assert.Equal("Gamma Gia / 6.001", result[1].Name);
        Assert.Equal([
            new StudentContractDetailResponse(1, 1, 2, 12, 72, 6), new StudentContractDetailResponse(2, 2, 3, 8, 80, 10)
        ], result[1].Tariffs);
        Assert.Equal([12], (await _repository.GetStudentContracts(10)).Select(c => c.ScId));
        Assert.Empty(await _repository.GetStudentContracts(9));
    }

    [Fact]
    public async Task Lookups_AreInTheOrderOfTheAccessForm()
    {
        Assert.Equal([new LookupItemResponse(1, "Monday"), new LookupItemResponse(3, "Wednesday")],
            await _repository.GetWeekDays());
        Assert.Equal([new LookupItemResponse(1, "08:00"), new LookupItemResponse(2, "08:30")],
            await _repository.GetLessonStartTimes());
        Assert.Equal([new LookupItemResponse(2, "A"), new LookupItemResponse(1, "B")], await _repository.GetRooms());
    }

    [Fact]
    public async Task GetStudentContractsForChange_ReturnsTrackedContracts()
    {
        List<StudentContract> result = await _repository.GetStudentContractsForChange([10, 12, 99]);

        Assert.Equal([10, 12], result.Select(c => c.ScId).Order());
        Assert.All(result, c => Assert.Equal(EntityState.Unchanged, _context.Entry(c).State));
    }

    [Fact]
    public void Add_AddsTheGroup()
    {
        var group = new Group { GrpId = 9, GroupCode = "901", AcademicYearId = 11 };

        _repository.Add(group);

        Assert.Equal(EntityState.Added, _context.Entry(group).State);
    }

    private Group LoadGroupWithRows(int grpId)
    {
        return _context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).Single(g => g.GrpId == grpId);
    }

    [Fact]
    public void Remove_DeletesTheGroupWithItsRows()
    {
        Group group = LoadGroupWithRows(1);

        _repository.Remove(group);
        _context.SaveChanges();

        Assert.False(_context.Groups.Any(g => g.GrpId == 1));
        Assert.False(_context.GroupsByTeachers.Any(t => t.GroupId == 1));
        Assert.False(_context.GroupsByStudents.Any(s => s.GroupId == 1));
        Assert.False(_context.GroupDayTimePlaces.Any(d => d.GroupId == 1));
        Assert.True(_context.GroupsByStudents.Any(s => s.GroupId == 2));
    }

    [Fact]
    public void RemoveRows_DeletesOnlyTheGivenRows()
    {
        Group group = LoadGroupWithRows(1);

        _repository.RemoveRows(new GroupRemovedRows([group.GroupsByTeachers.Single(t => t.Id == 11)],
            [group.GroupsByStudents.Single(s => s.GbsId == 22)], []));
        _context.SaveChanges();

        Assert.Equal([12], _context.GroupsByTeachers.Where(t => t.GroupId == 1).Select(t => t.Id));
        Assert.Equal([21, 23], _context.GroupsByStudents.Where(s => s.GroupId == 1).Select(s => s.GbsId).Order());
        Assert.Equal(2, _context.GroupDayTimePlaces.Count(d => d.GroupId == 1));
    }
}
