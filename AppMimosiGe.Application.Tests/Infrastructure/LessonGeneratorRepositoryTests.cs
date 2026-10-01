using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class LessonGeneratorRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly LessonGeneratorRepository _repository;

    public LessonGeneratorRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new LessonGeneratorRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Date(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //groups 1 ("1001", dirty, with a teacher, a student, a schedule row, lesson 100 and two log entries),
    //2 ("0901", lesson 101 and one log entry) and 3 ("1101", dirty). Synthetic data only
    private void Seed()
    {
        _context.ErrorLogTexts.AddRange(new ErrorLogText { EltId = 6, Text = "no teacher" },
            new ErrorLogText { EltId = 14, Text = "extra student" });
        _context.OperationMonths.AddRange(new OperationMonth { Id = 1, MonthDate = Date(9, 1) },
            new OperationMonth { Id = 2, MonthDate = Date(10, 1) });
        _context.Groups.AddRange(Group(1, "1001", true), Group(2, "0901", false), Group(3, "1101", true));
        _context.StudentContracts.AddRange(Contract(10, "6.001"), Contract(11, "6.002"));
        _context.LessonStartTimes.Add(new LessonStartTime { LstId = 1, LstTime = new TimeOnly(15, 0) });
        _context.GroupsByTeachers.Add(new GroupByTeacher
        {
            Id = 11, GroupId = 1, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Date(9, 1)
        });
        _context.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 21, GroupId = 1, StudentContractId = 10, StartDate = Date(9, 1)
        });
        _context.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 31,
            GroupId = 1,
            WeekDayId = 1,
            LessonStartTimeId = 1,
            HoursCount = 1.5f,
            RoomId = 1,
            StartDate = Date(9, 1)
        });
        _context.Lessons.AddRange(Lesson(100, 1, Date(9, 7, 15)), Lesson(101, 2, Date(9, 8, 15)));
        _context.LessonsByStudents.AddRange(
            new LessonByStudent { Id = 200, LessonId = 100, StudentContractId = 10, GroupByStudentId = 21 },
            new LessonByStudent { Id = 201, LessonId = 100, StudentContractId = 11, Present = true });
        _context.LessonsCheckCreateErrorLogs.AddRange(Log(1, 1, 6, Date(9, 14), null),
            Log(2, 1, 14, Date(9, 7, 15), 100), Log(3, 2, 6, Date(9, 9), null), Log(4, 1, 6, null, null));
        _context.SaveChanges();
    }

    private static Group Group(int id, string code, bool dirtyLessons)
    {
        return new Group
        {
            GrpId = id,
            GroupCode = code,
            AcademicYearId = 11,
            CourseId = 1,
            StudentStatusId = 1,
            DirtyLessons = dirtyLessons
        };
    }

    private static StudentContract Contract(int id, string number)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = 1,
            PayerHumanId = 1,
            AcademicYearId = 11,
            DirtyNextPayDate = false
        };
    }

    private static Lesson Lesson(int id, int groupId, DateTime lessonDt)
    {
        return new Lesson
        {
            Id = id,
            GroupId = groupId,
            LessonDt = lessonDt,
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 8f,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
    }

    private static LessonCheckCreateErrorLog Log(int id, int groupId, int errorCode, DateTime? lessonDate,
        int? lessonId)
    {
        return new LessonCheckCreateErrorLog
        {
            Id = id,
            CreatedDate = Date(9, 30),
            GroupId = groupId,
            ErrorLogTextId = errorCode,
            LessonDate = lessonDate,
            LessonId = lessonId
        };
    }

    //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set
    private void SaveAndForget()
    {
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetLastOperationMonth_ReturnsTheLatestMonth()
    {
        Assert.Equal(Date(10, 1), await _repository.GetLastOperationMonth());
    }

    [Fact]
    public async Task GetLastOperationMonth_EmptyCalendar_ReturnsNull()
    {
        _context.OperationMonths.RemoveRange(_context.OperationMonths);
        SaveAndForget();

        Assert.Null(await _repository.GetLastOperationMonth());
    }

    [Fact]
    public void AddOperationMonths_AddsTheMonths()
    {
        _repository.AddOperationMonths([Date(11, 1), Date(12, 1)]);
        SaveAndForget();

        Assert.Equal([Date(9, 1), Date(10, 1), Date(11, 1), Date(12, 1)],
            _context.OperationMonths.OrderBy(m => m.MonthDate).Select(m => m.MonthDate));
    }

    [Fact]
    public async Task MarkAllDirty_MarksEveryGroupAndStudentContract()
    {
        await _repository.MarkAllDirty();
        SaveAndForget();

        Assert.All(_context.Groups, g => Assert.True(g.DirtyLessons));
        Assert.All(_context.StudentContracts, c => Assert.True(c.DirtyNextPayDate));
    }

    [Theory]
    [InlineData(true, new[] { 1, 3 })]
    [InlineData(false, new[] { 1, 2, 3 })]
    public async Task GetGroupIds_ReturnsTheGroupsInIdOrder(bool onlyDirty, int[] expected)
    {
        Assert.Equal(expected, await _repository.GetGroupIds(onlyDirty));
    }

    [Fact]
    public async Task GetGroupForGeneration_LoadsTheRowsTheScheduleTimesTheLessonsAndTheLog()
    {
        Group? group = await _repository.GetGroupForGeneration(1, false);

        Assert.NotNull(group);
        Assert.Equal(11, Assert.Single(group.GroupsByTeachers).Id);
        Assert.Equal(21, Assert.Single(group.GroupsByStudents).GbsId);
        Assert.Equal(new TimeOnly(15, 0), Assert.Single(group.GroupDayTimePlaces).LessonStartTime.LstTime);
        Lesson lesson = Assert.Single(group.Lessons);
        Assert.Equal([200, 201], lesson.LessonsByStudents.Select(s => s.Id).Order());
        Assert.Equal([1, 2, 4], group.LessonsCheckCreateErrorLogs.Select(l => l.Id).Order());
        Assert.Equal(EntityState.Detached, _context.Entry(group).State);
    }

    [Fact]
    public async Task GetGroupForGeneration_ForChange_TracksTheGroup()
    {
        Group? group = await _repository.GetGroupForGeneration(1, true);

        Assert.NotNull(group);
        Assert.Equal(EntityState.Unchanged, _context.Entry(group).State);
        Assert.Equal(EntityState.Unchanged, _context.Entry(group.Lessons.Single()).State);
    }

    [Fact]
    public async Task GetGroupForGeneration_UnknownGroup_ReturnsNull()
    {
        Assert.Null(await _repository.GetGroupForGeneration(99, false));
    }

    [Fact]
    public async Task GetStudentContractsForChange_ReturnsTrackedContracts()
    {
        List<StudentContract> contracts = await _repository.GetStudentContractsForChange([11, 99]);

        StudentContract contract = Assert.Single(contracts);
        Assert.Equal(11, contract.ScId);
        Assert.Equal(EntityState.Unchanged, _context.Entry(contract).State);
    }

    [Fact]
    public async Task GetErrorLogTexts_ReturnsTheTextsByCode()
    {
        Assert.Equal(new Dictionary<int, string> { [6] = "no teacher", [14] = "extra student" },
            await _repository.GetErrorLogTexts());
    }

    [Fact]
    public async Task GetLog_OrdersByGroupCodeThenLessonDate()
    {
        List<LessonGeneratorLogRowResponse> log = await _repository.GetLog(null);

        Assert.Equal([3, 4, 2, 1], log.Select(l => l.Id));
        Assert.Equal(new LessonGeneratorLogRowResponse(2, Date(9, 30), 1, "1001", 14, "extra student",
            Date(9, 7, 15), 100), log[2]);
    }

    [Fact]
    public async Task GetLog_OneGroup_ReturnsOnlyItsEntries()
    {
        List<LessonGeneratorLogRowResponse> log = await _repository.GetLog(2);

        Assert.Equal([3], log.Select(l => l.Id));
    }

    [Fact]
    public void AddLesson_SavesTheLessonWithItsStudents()
    {
        Lesson lesson = Lesson(0, 3, Date(9, 14, 15));
        lesson.LessonsByStudents.Add(new LessonByStudent { StudentContractId = 10, HoursCount = 2f });

        _repository.AddLesson(lesson);
        SaveAndForget();

        Lesson saved = _context.Lessons.Include(l => l.LessonsByStudents).Single(l => l.GroupId == 3);
        Assert.Equal(Date(9, 14, 15), saved.LessonDt);
        Assert.Equal(2f, Assert.Single(saved.LessonsByStudents).HoursCount);
    }

    [Fact]
    public async Task RemoveLesson_DeletesTheLessonAndItsStudents()
    {
        Group group = (await _repository.GetGroupForGeneration(1, true))!;

        _repository.RemoveLesson(group.Lessons.Single());
        SaveAndForget();

        Assert.Equal([101], _context.Lessons.Select(l => l.Id));
        Assert.Empty(_context.LessonsByStudents);
    }

    [Fact]
    public async Task RemoveLessonStudent_DeletesOnlyThatRow()
    {
        Group group = (await _repository.GetGroupForGeneration(1, true))!;

        _repository.RemoveLessonStudent(group.Lessons.Single().LessonsByStudents.Single(s => s.Id == 201));
        SaveAndForget();

        Assert.Equal([200], _context.LessonsByStudents.Select(s => s.Id));
    }

    [Fact]
    public async Task ReplaceLogs_ReplacesOnlyTheGroupEntries()
    {
        Group group = (await _repository.GetGroupForGeneration(1, true))!;

        _repository.ReplaceLogs(group, [Log(0, 1, 14, Date(9, 21, 15), 100)]);
        SaveAndForget();

        Assert.Equal([(1, 14), (2, 6)],
            _context.LessonsCheckCreateErrorLogs.AsEnumerable().Select(l => (l.GroupId, l.ErrorLogTextId)).Order());
    }
}
