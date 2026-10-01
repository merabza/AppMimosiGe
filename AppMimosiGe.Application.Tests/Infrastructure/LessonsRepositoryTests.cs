using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class LessonsRepositoryTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 1, 13, 0, 0, DateTimeKind.Unspecified);

    private readonly MimosiGeDbContext _context;
    private readonly LessonsRepository _repository;

    public LessonsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new LessonsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //groups 1 ("1001", Math) and 2 ("201", English, last year); teachers 1 and 2. Lessons of group 1: 100 (29.09 15:00,
    //one of two present), 101 (30.09 15:00, nobody present), 102 (01.10 15:00, after Now, nobody present), 103
    //(30.09 10:00, cancelled, substitute 2); lesson 200 of group 2 (30.09 15:00, no students).
    //Synthetic people only: never real names
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
        _context.Courses.AddRange(new Course { CrsId = 1, CourseName = "Math" },
            new Course { CrsId = 2, CourseName = "English" });
        _context.GroupSizes.Add(new GroupSize { GrsId = 1, GrsSize = 4, GrsName = "Four" });
        _context.StudentStatuses.Add(new StudentStatus { Id = 1, StudentStatusName = "Tenth", Rate = 110 });
        _context.Humans.AddRange(Human(1, "Alpha", "Ann"), Human(2, "Beta", "Bob"), Human(3, "Gamma", "Gia"),
            Human(4, "Delta", "Dan"), Human(5, "Epsilon", "Eva"));
        _context.TeacherSalarySchemes.Add(new TeacherSalaryScheme { Id = 1, SchemaName = "Senior" });
        _context.RsCountries.Add(new RsCountry { Id = 1, Code = "GE", CountryName = "Georgia" });
        _context.TeacherContracts.AddRange(
            new TeacherContract { Id = 1, ContractNumber = "T3.02", TeacherHumanId = 2, RsCountryId = 1 },
            new TeacherContract { Id = 2, ContractNumber = "T3.01", TeacherHumanId = 1, RsCountryId = 1 });
        _context.LessonStatuses.AddRange(new LessonStatus { Id = 2, StatusName = "cancelled" },
            new LessonStatus { Id = 1, StatusName = "held" });
        //contract 13 is in no group and on no lesson
        _context.StudentContracts.AddRange(StudentContract(10, "6.001", 3), StudentContract(11, "6.002", 4),
            StudentContract(12, "6.003", 5), StudentContract(13, "6.004", 4));
        _context.Groups.AddRange(Group(1, "1001", 11, 1), Group(2, "201", 10, 2));
        _context.GroupsByStudents.AddRange(GroupStudent(31, 1, 10), GroupStudent(32, 1, 11), GroupStudent(33, 2, 12));
        _context.Lessons.AddRange(Lesson(100, 1, At(9, 29, 15), 1), Lesson(101, 1, At(9, 30, 15), 1),
            Lesson(102, 1, At(10, 1, 15), 1), Lesson(103, 1, At(9, 30, 10), 2, 2), Lesson(200, 2, At(9, 30, 15), 1));
        _context.LessonsByStudents.AddRange(Row(1, 100, 10, true), Row(2, 100, 11, false), Row(3, 101, 11, false),
            Row(4, 101, 10, false), Row(5, 102, 10, false), Row(6, 103, 10, false));
        _context.SaveChanges();
    }

    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    //the lesson-level journal fields of cancelled lesson 103 and a theme for its student
    private void FillLesson103()
    {
        Lesson lesson = _context.Lessons.Single(l => l.Id == 103);
        lesson.TeacherLateMinutes = 7;
        lesson.RecoverDate = At(10, 5);
        lesson.Note = "note";
        _context.LessonsByStudents.Single(s => s.Id == 6).Theme = "theme";
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };
    }

    private static StudentContract StudentContract(int id, string number, int studentId)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = studentId,
            PayerHumanId = studentId,
            AcademicYearId = 11,
            DirtyNextPayDate = false
        };
    }

    private static Group Group(int id, string code, int academicYearId, int courseId)
    {
        return new Group
        {
            GrpId = id,
            GroupCode = code,
            AcademicYearId = academicYearId,
            CourseId = courseId,
            GroupSizeId = 1,
            StudentStatusId = 1
        };
    }

    private static GroupByStudent GroupStudent(int id, int groupId, int studentContractId)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = groupId,
            StudentContractId = studentContractId,
            FourWeekHours = 8,
            FourWeekFee = 80,
            OneHourFee = 10,
            HoursCoefficient = 1,
            StartDate = At(9, 1)
        };
    }

    private static Lesson Lesson(int id, int groupId, DateTime lessonDt, int statusId, int? substituteId = null)
    {
        return new Lesson
        {
            Id = id,
            GroupId = groupId,
            LessonDt = lessonDt,
            TeacherContractId = groupId,
            SubstituteTeacherContractId = substituteId,
            SalarySchemaId = 1,
            FourWeekHours = 8f,
            LessonStatusId = statusId,
            TeoMinDate = lessonDt.AddDays(-1),
            TeoMaxDate = lessonDt.AddDays(1)
        };
    }

    private static LessonByStudent Row(int id, int lessonId, int studentContractId, bool present)
    {
        return new LessonByStudent
        {
            Id = id,
            LessonId = lessonId,
            StudentContractId = studentContractId,
            Present = present,
            HoursCount = 1.5f
        };
    }

    private static LessonsListQuery Query(int? grpId = null, int? teacherContractId = null, DateTime? dateFrom = null,
        DateTime? dateTo = null, int? lessonStatusId = null, bool unfilled = false,
        IReadOnlyList<LessonSortField>? sortFields = null, int offset = 0, int rowsCount = 100)
    {
        return new LessonsListQuery(offset, rowsCount, Now, grpId, teacherContractId, dateFrom, dateTo, lessonStatusId,
            unfilled, sortFields ?? LessonsListQueryFactory.DefaultSortFields);
    }

    private async Task<int[]> Ids(LessonsListQuery query)
    {
        return [.. (await _repository.GetRowsData(query)).Rows.Select(r => r.LessonId)];
    }

    private async Task AssertIds(LessonsListQuery query, params int[] expected)
    {
        int[] ids = await Ids(query);
        Assert.Equal(expected, ids);
    }

    [Fact]
    public async Task GetRowsData_ListsAllLessonsByTimeThenGroupCode()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal([100, 103, 101, 200, 102], data.Rows.Select(r => r.LessonId));
    }

    [Fact]
    public async Task GetRowsData_RowHasGroupTeacherSubstituteStatusAndCounts()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query());

        Assert.Equal(new LessonRowResponse(100, At(9, 29, 15), 1, "1001", "Math", "Beta Bob", null, 1, "held", 2, 1),
            data.Rows.Single(r => r.LessonId == 100));
        Assert.Equal(
            new LessonRowResponse(103, At(9, 30, 10), 1, "1001", "Math", "Beta Bob", "Alpha Ann", 2, "cancelled", 1, 0),
            data.Rows.Single(r => r.LessonId == 103));
        Assert.Equal(new LessonRowResponse(200, At(9, 30, 15), 2, "201", "English", "Alpha Ann", null, 1, "held", 0, 0),
            data.Rows.Single(r => r.LessonId == 200));
    }

    [Fact]
    public async Task GetRowsData_FiltersByGroup()
    {
        await AssertIds(Query(2), 200);
    }

    // the teacher filter finds a lesson by its teacher and by its substitute
    [Fact]
    public async Task GetRowsData_FiltersByTeacherOrSubstitute()
    {
        await AssertIds(Query(teacherContractId: 2), 103, 200);
        await AssertIds(Query(teacherContractId: 1), 100, 103, 101, 102);
    }

    // both ends of the range are whole days
    [Fact]
    public async Task GetRowsData_FiltersByDaysIncludingBothEnds()
    {
        await AssertIds(Query(dateFrom: At(9, 30), dateTo: At(9, 30)), 103, 101, 200);
        await AssertIds(Query(dateFrom: At(9, 30)), 103, 101, 200, 102);
        await AssertIds(Query(dateTo: At(9, 29)), 100);
    }

    [Fact]
    public async Task GetRowsData_FiltersByStatus()
    {
        await AssertIds(Query(lessonStatusId: 2), 103);
    }

    // unfilled: started, held (status 1), with students, nobody present. 100 has a present student, 102 has not
    // started, 103 is cancelled, 200 has no students
    [Fact]
    public async Task GetRowsData_Unfilled_ListsPastHeldLessonsWithoutPresentStudents()
    {
        await AssertIds(Query(unfilled: true), 101);
    }

    [Theory]
    [InlineData(ELessonSortField.LessonDt, false, new[] { 102, 101, 200, 103, 100 })]
    [InlineData(ELessonSortField.GroupCode, false, new[] { 200, 100, 101, 102, 103 })]
    [InlineData(ELessonSortField.CourseName, true, new[] { 200, 100, 101, 102, 103 })]
    [InlineData(ELessonSortField.TeacherName, false, new[] { 100, 101, 102, 103, 200 })]
    [InlineData(ELessonSortField.SubstituteTeacherName, false, new[] { 103, 100, 101, 102, 200 })]
    [InlineData(ELessonSortField.LessonStatusName, false, new[] { 103, 100, 101, 102, 200 })]
    [InlineData(ELessonSortField.StudentsCount, true, new[] { 200, 102, 103, 100, 101 })]
    [InlineData(ELessonSortField.PresentCount, false, new[] { 100, 101, 102, 103, 200 })]
    public async Task GetRowsData_SortsByEachFieldThenById(ELessonSortField field, bool ascending, int[] expected)
    {
        Assert.Equal(expected, await Ids(Query(sortFields: [new LessonSortField(field, ascending)])));
    }

    [Fact]
    public async Task GetRowsData_ReturnsTheRequestedPage()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 2, rowsCount: 2));

        Assert.Equal(5, data.AllRowsCount);
        Assert.Equal(2, data.Offset);
        Assert.Equal([101, 200], data.Rows.Select(r => r.LessonId));
    }

    // a page past the end (after a filter change) shows the last page
    [Fact]
    public async Task GetRowsData_OffsetPastTheEnd_ShowsTheLastPage()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 10, rowsCount: 2));

        Assert.Equal(4, data.Offset);
        Assert.Equal([102], data.Rows.Select(r => r.LessonId));
    }

    // an offset equal to the count is past the end too
    [Fact]
    public async Task GetRowsData_OffsetAtTheCount_ShowsTheLastPage()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query(offset: 5, rowsCount: 5));

        Assert.Equal(0, data.Offset);
        Assert.Equal(5, data.Rows.Count);
    }

    [Fact]
    public async Task GetRowsData_NoRows_KeepsOffsetZero()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query(99, offset: 0));

        Assert.Equal(0, data.AllRowsCount);
        Assert.Equal(0, data.Offset);
        Assert.Empty(data.Rows);
    }

    // with no rows there is no last page to move to: the requested offset comes back
    [Fact]
    public async Task GetRowsData_NoRows_KeepsTheRequestedOffset()
    {
        LessonsRowsDataResponse data = await _repository.GetRowsData(Query(99, offset: 20, rowsCount: 10));

        Assert.Equal(0, data.AllRowsCount);
        Assert.Equal(20, data.Offset);
    }

    // a later sort field orders within the equal values of the earlier one, descending too
    [Fact]
    public async Task GetRowsData_SecondSortField_OrdersWithinTheFirst()
    {
        await AssertIds(Query(sortFields:
        [
            new LessonSortField(ELessonSortField.GroupCode, true),
            new LessonSortField(ELessonSortField.LessonDt, false)
        ]), 102, 101, 103, 100, 200);
    }

    [Fact]
    public async Task GetRowsData_UnknownSortField_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repository.GetRowsData(Query(sortFields: [new LessonSortField((ELessonSortField)99, true)])));

        Assert.Contains("უცნობი დალაგების ველი", exception.Message, StringComparison.Ordinal);
    }

    // the last day ends before midnight: a lesson at 00:00 of the next day is not in the range
    [Fact]
    public async Task GetRowsData_DateTo_ExcludesMidnightOfTheNextDay()
    {
        AddAndSave(Lesson(104, 1, At(10, 2), 1));

        await AssertIds(Query(dateFrom: At(10, 1), dateTo: At(10, 1)), 102);
        await AssertIds(Query(dateFrom: At(10, 2), dateTo: At(10, 2)), 104);
    }

    // a lesson that starts right now has not started yet: it is not unfilled
    [Fact]
    public async Task GetRowsData_Unfilled_LessonStartingNow_IsNotUnfilled()
    {
        AddAndSave(Lesson(104, 1, Now, 1), Row(7, 104, 10, false));

        await AssertIds(Query(unfilled: true), 101);
    }

    [Fact]
    public async Task GetOne_ReturnsTheLessonWithStudentsByNameAndNeighbours()
    {
        LessonResponse? lesson = await _repository.GetOne(101);

        Assert.NotNull(lesson);
        Assert.Equal(1, lesson.GrpId);
        Assert.Equal("1001", lesson.GroupCode);
        Assert.Equal("Math", lesson.CourseName);
        Assert.Equal(1, lesson.TeacherContractId);
        Assert.Equal("Beta Bob / T3.02", lesson.TeacherName);
        Assert.Equal(At(9, 30, 15), lesson.LessonDt);
        Assert.Equal("Senior", lesson.SalarySchemeName);
        Assert.Equal(8f, lesson.FourWeekHours);
        Assert.Equal(At(9, 29, 15), lesson.TeoMinDate);
        Assert.Equal(At(10, 1, 15), lesson.TeoMaxDate);
        Assert.Equal(1, lesson.LessonStatusId);
        //the previous lesson of the group is 103 (same day, earlier), the next 102; 200 is another group's
        Assert.Equal(103, lesson.PreviousLessonId);
        Assert.Equal(102, lesson.NextLessonId);
        Assert.Equal([3, 4], lesson.Students.Select(s => s.Id));
        Assert.Equal(new LessonStudentResponse(3, 11, "Delta Dan", 1.5f, false, null, null, null, null, 0),
            lesson.Students[0]);
        Assert.Equal("Gamma Gia", lesson.Students[1].StudentName);
    }

    [Fact]
    public async Task GetOne_FirstAndLastLessonsOfTheGroup_HaveNoNeighbourOnThatSide()
    {
        LessonResponse? first = await _repository.GetOne(100);
        LessonResponse? last = await _repository.GetOne(102);

        Assert.Null(first!.PreviousLessonId);
        Assert.Equal(103, first.NextLessonId);
        Assert.Equal(101, last!.PreviousLessonId);
        Assert.Null(last.NextLessonId);
    }

    [Fact]
    public async Task GetOne_EditableFieldsAreReturned()
    {
        FillLesson103();

        LessonResponse? result = await _repository.GetOne(103);

        Assert.Equal(2, result!.LessonStatusId);
        Assert.Equal(2, result.SubstituteTeacherContractId);
        Assert.Equal(7, result.TeacherLateMinutes);
        Assert.Equal(At(10, 5), result.RecoverDate);
        Assert.Equal("note", result.Note);
        Assert.Equal("theme", Assert.Single(result.Students).Theme);
    }

    [Fact]
    public async Task GetOne_MissingLesson_IsNull()
    {
        Assert.Null(await _repository.GetOne(999));
    }

    // "last first" with a space between ("A Z" before "Ab A"), equal names by the row id
    [Fact]
    public async Task GetOne_StudentsByFullNameThenByRowId()
    {
        AddAndSave(Human(6, "A", "Z"), Human(7, "Ab", "A"), Human(8, "Ab", "A"), StudentContract(14, "6.014", 6),
            StudentContract(15, "6.015", 7), StudentContract(16, "6.016", 8), Lesson(105, 1, At(10, 5, 15), 1),
            Row(13, 105, 15, false), Row(12, 105, 16, false), Row(14, 105, 14, false));

        LessonResponse? lesson = await _repository.GetOne(105);

        Assert.Equal([14, 12, 13], lesson!.Students.Select(s => s.Id));
        Assert.Equal(["A Z", "Ab A", "Ab A"], lesson.Students.Select(s => s.StudentName));
    }

    [Fact]
    public async Task GetForChange_LoadsTheTrackedLessonWithItsRows()
    {
        Lesson? lesson = await _repository.GetForChange(100);

        Assert.NotNull(lesson);
        Assert.Equal([1, 2], lesson.LessonsByStudents.Select(s => s.Id).Order());
        Assert.Equal(EntityState.Unchanged, _context.Entry(lesson).State);
        Assert.Null(await _repository.GetForChange(999));
    }

    [Fact]
    public async Task Exists_ChecksStatusesAndTeacherContracts()
    {
        Assert.True(await _repository.LessonStatusExists(2));
        Assert.False(await _repository.LessonStatusExists(3));
        Assert.True(await _repository.TeacherContractExists(1));
        Assert.False(await _repository.TeacherContractExists(3));
    }

    [Fact]
    public async Task GetGroups_NewerYearsFirstWithYearInTheName()
    {
        Assert.Equal([new LookupItemResponse(1, "1001 / 2026-2027"), new LookupItemResponse(2, "201 / 2025-2026")],
            await _repository.GetGroups());
    }

    // within the years of one start date by code, one code of two such years by the group id
    [Fact]
    public async Task GetGroups_SameStartDate_ByCodeThenById()
    {
        AddAndSave(
            new AcademicYear
            {
                AyId = 12, AcademicYearName = "2026-2027 B", StartDate = At(9, 1), FinishDate = At(9, 1).AddYears(1)
            }, Group(5, "1001", 12, 1), Group(6, "0901", 11, 1));

        Assert.Equal([6, 1, 5, 2], (await _repository.GetGroups()).Select(g => g.Id));
    }

    [Fact]
    public async Task GetTeacherContracts_ByNameWithContractNumber()
    {
        Assert.Equal([new LookupItemResponse(2, "Alpha Ann / T3.01"), new LookupItemResponse(1, "Beta Bob / T3.02")],
            await _repository.GetTeacherContracts());
    }

    [Fact]
    public async Task GetLessonStatuses_ById()
    {
        Assert.Equal([new LookupItemResponse(1, "held"), new LookupItemResponse(2, "cancelled")],
            await _repository.GetLessonStatuses());
    }

    // the group's students and the lesson's own students: contract 12 is in group 2 only
    [Fact]
    public async Task GetStudentContractsForChange_LoadsTheGroupsAndTheLessonsContractsTracked()
    {
        AddAndSave(Row(7, 100, 12, false));

        List<StudentContract> group1 = await _repository.GetStudentContractsForChange(1, 101);
        List<StudentContract> withOwnRow = await _repository.GetStudentContractsForChange(1, 100);

        Assert.Equal([10, 11], group1.Select(c => c.ScId).Order());
        Assert.Equal([10, 11, 12], withOwnRow.Select(c => c.ScId).Order());
        Assert.All(withOwnRow, c => Assert.Equal(EntityState.Unchanged, _context.Entry(c).State));
    }
}
