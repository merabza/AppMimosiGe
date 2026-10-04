using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

// the lessons reports' data (part 17): lessons of a period, the generator log, theoretical dates, absences
public sealed class ReportsRepositoryLessonsTests : IDisposable
{
    private static readonly DateTime September = Day(2026, 9, 1);
    private static readonly DateTime October = Day(2026, 10, 1);

    private readonly MimosiGeDbContext _context;
    private readonly ReportsRepository _repository;

    public ReportsRepositoryLessonsTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new ReportsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Day(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    //groups 100 A1 (Math) and 101 B1 (Art), both active from September with a teacher and a schedule row; student
    //contracts 20 (in A1) and 21 (in A1 and B1). Lessons: 1 Sep 10 (substitute, a present and an absent student),
    //2 Sep 11 cancelled with a recovery date, 3 Sep 30 23:00 (B1), 4 Oct 1 00:00, 5 Sep 1 00:00.
    //Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(Human(1, "Zeta", "Ann", null), Human(2, "Alpha", "Bob", null),
            Human(3, "Gamma", "Cid", "555000003"), Human(4, "Beta", "Dan", "555000004"),
            Human(5, "Delta", "Eve", "555000005"));
        _context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T5", TeacherHumanId = 1, RsCountryId = 1 },
            new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 2, RsCountryId = 1 });
        _context.StudentContracts.AddRange(StudentContract(20, "6.020", 3, 5), StudentContract(21, "6.021", 4, 4));
        _context.Courses.AddRange(new Course { CrsId = 3, CourseName = "Math" },
            new Course { CrsId = 4, CourseName = "Art" });
        _context.Groups.AddRange(new Group { GrpId = 100, GroupCode = "A1", CourseId = 3 },
            new Group { GrpId = 101, GroupCode = "B1", CourseId = 4 });
        _context.LessonStatuses.AddRange(new LessonStatus { Id = 1, StatusName = "held" },
            new LessonStatus { Id = 2, StatusName = "cancelled" }, new LessonStatus { Id = 3, StatusName = "other" });
        _context.ErrorLogTexts.AddRange(new ErrorLogText { EltId = 6, Text = "no teacher" },
            new ErrorLogText { EltId = 11, Text = "extra lesson" }, new ErrorLogText { EltId = 14, Text = "extra student" });
        _context.GroupsByStudents.AddRange(GroupStudent(1, 100, 20, September, null),
            GroupStudent(2, 100, 21, September, null), GroupStudent(3, 101, 21, September, null));
        _context.GroupsByTeachers.AddRange(GroupTeacher(1, 100, 5), GroupTeacher(2, 101, 6));
        _context.GroupDayTimePlaces.AddRange(Schedule(1, 100), Schedule(2, 101));
        _context.Lessons.AddRange(
            Lesson(1, 100, 5, September.AddDays(9).AddHours(10), 1, 6, null, September.AddHours(10),
                Day(2026, 9, 29).AddHours(10)),
            Lesson(2, 100, 5, September.AddDays(10).AddHours(10), 2, null, Day(2026, 9, 20), September.AddHours(10),
                September),
            Lesson(3, 101, 6, Day(2026, 9, 30).AddHours(23), 1, null, null, Day(2026, 9, 3), Day(2026, 9, 30).AddHours(23)),
            Lesson(4, 101, 6, October, 1, null, null, October.AddSeconds(1), Day(2026, 10, 29).AddHours(10)),
            Lesson(5, 100, 5, September, 3, null, null, September.AddHours(10), Day(2026, 9, 29).AddHours(10)));
        _context.LessonsByStudents.AddRange(LessonStudent(1, 1, 20, 1, true), LessonStudent(2, 1, 21, 2, false),
            LessonStudent(3, 2, 20, 1, false), LessonStudent(4, 3, 21, 3, false), LessonStudent(5, 4, 21, 3, false),
            LessonStudent(6, 5, 20, 1, false), LessonStudent(7, 5, 21, 2, false));
        _context.SaveChanges();
    }

    private void Save(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static Human Human(int id, string lastName, string firstName, string? phone)
    {
        return new Human
        {
            HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}", PhoneNumber = phone
        };
    }

    private static StudentContract StudentContract(int id, string number, int studentId, int payerId)
    {
        return new StudentContract
        {
            ScId = id, ContractNumber = number, StudentHumanId = studentId, PayerHumanId = payerId, AcademicYearId = 1
        };
    }

    private static GroupByStudent GroupStudent(int id, int groupId, int contractId, DateTime start, DateTime? end)
    {
        return new GroupByStudent
        {
            GbsId = id, GroupId = groupId, StudentContractId = contractId, StartDate = start, EndDate = end
        };
    }

    private static GroupByTeacher GroupTeacher(int id, int groupId, int contractId)
    {
        return new GroupByTeacher { Id = id, GroupId = groupId, TeacherContractId = contractId, StartDate = September };
    }

    private static GroupDayTimePlace Schedule(int id, int groupId)
    {
        return new GroupDayTimePlace
        {
            GdtpId = id, GroupId = groupId, WeekDayId = 1, LessonStartTimeId = 1, RoomId = 1, StartDate = September
        };
    }

    private static Lesson Lesson(int id, int groupId, int teacherId, DateTime lessonDt, int statusId,
        int? substituteId, DateTime? recoverDate, DateTime teoMinDate, DateTime teoMaxDate)
    {
        return new Lesson
        {
            Id = id,
            GroupId = groupId,
            TeacherContractId = teacherId,
            SubstituteTeacherContractId = substituteId,
            LessonDt = lessonDt,
            LessonStatusId = statusId,
            RecoverDate = recoverDate,
            TeoMinDate = teoMinDate,
            TeoMaxDate = teoMaxDate,
            FourWeekHours = 8
        };
    }

    private static LessonByStudent LessonStudent(int id, int lessonId, int contractId, int? groupStudentId,
        bool present)
    {
        return new LessonByStudent
        {
            Id = id, LessonId = lessonId, StudentContractId = contractId, GroupByStudentId = groupStudentId,
            Present = present
        };
    }

    // [from, to): a lesson at the start is in, one at the end is not; teachers and substitutes with their numbers
    [Fact]
    public async Task GetPeriodLessons_LoadsTheLessonsOfThePeriod()
    {
        // Act
        List<PeriodLessonRow> lessons = await _repository.GetPeriodLessons(September, October);

        // Assert
        var teacher5 = new SchedulePerson("Zeta", "Ann", "T5");
        Assert.Equal([
            new PeriodLessonRow(1, Day(2026, 9, 10).AddHours(10), "A1", 5, teacher5,
                new SchedulePerson("Alpha", "Bob", "T6"), 1, "held", false, true),
            new PeriodLessonRow(2, Day(2026, 9, 11).AddHours(10), "A1", 5, teacher5, null, 2, "cancelled", true, false),
            new PeriodLessonRow(3, Day(2026, 9, 30).AddHours(23), "B1", 6, new SchedulePerson("Alpha", "Bob", "T6"),
                null, 1, "held", false, false),
            new PeriodLessonRow(5, September, "A1", 5, teacher5, null, 3, "other", false, false)
        ], lessons.OrderBy(l => l.LessonId));
    }

    [Fact]
    public async Task GetPeriodLessons_EmptyPeriod_IsEmpty()
    {
        Assert.Empty(await _repository.GetPeriodLessons(October.AddHours(1), October.AddDays(1)));
    }

    // only log rows with a lesson of the same group (Access's INNER JOIN on the lesson and the group)
    [Fact]
    public async Task GetLessonErrors_LoadsTheLogRowsWithALessonOfTheGroup()
    {
        // Arrange
        Save(Log(1, 100, 1, 11), Log(2, 100, 3, 14), Log(3, 100, null, 6), Log(4, 101, 3, 14));

        // Act
        List<LessonErrorRow> errors = await _repository.GetLessonErrors();

        // Assert
        Assert.Equal([
            new LessonErrorRow(1, "A1", Day(2026, 9, 10).AddHours(10), "extra lesson"),
            new LessonErrorRow(4, "B1", Day(2026, 9, 30).AddHours(23), "extra student")
        ], errors.OrderBy(e => e.LogId));
        return;

        static LessonCheckCreateErrorLog Log(int id, int groupId, int? lessonId, int errorId) => new()
        {
            Id = id, CreatedDate = October, GroupId = groupId, LessonId = lessonId, ErrorLogTextId = errorId
        };
    }

    // a theoretical date at 00:00:00 (hours, minutes and seconds); 00:00:01 is not
    [Fact]
    public async Task GetLessonsWithMidnightTeoDates_LoadsTheLessonsWithAMidnightDate()
    {
        // Act
        List<TeoDatesLessonRow> lessons = await _repository.GetLessonsWithMidnightTeoDates();

        // Assert
        Assert.Equal([
            new TeoDatesLessonRow(2, "A1", new SchedulePerson("Zeta", "Ann", "T5"), September.AddHours(10), September,
                8f, Day(2026, 9, 11).AddHours(10)),
            new TeoDatesLessonRow(3, "B1", new SchedulePerson("Alpha", "Bob", "T6"), Day(2026, 9, 3),
                Day(2026, 9, 30).AddHours(23), 8f, Day(2026, 9, 30).AddHours(23))
        ], lessons.OrderBy(l => l.LessonId));
    }

    // absent students of not cancelled lessons in [from, to), by contract and the course of the lesson's group;
    // status 3 counts (Access: Status <> 2)
    [Fact]
    public async Task GetAbsenceCounts_CountsByContractAndCourse()
    {
        // Act
        List<AbsenceCountRow> counts = await _repository.GetAbsenceCounts(September, October);

        // Assert
        Assert.Equal([
            new AbsenceCountRow(20, new SchedulePerson("Gamma", "Cid", "6.020"), "Math", 1),
            new AbsenceCountRow(21, new SchedulePerson("Beta", "Dan", "6.021"), "Art", 1),
            new AbsenceCountRow(21, new SchedulePerson("Beta", "Dan", "6.021"), "Math", 2)
        ], counts.OrderBy(c => c.StudentContractId).ThenBy(c => c.CourseName, StringComparer.Ordinal));
    }

    // the limit cuts the period: here the lessons before September 10th, 12:00
    [Fact]
    public async Task GetAbsenceCounts_UntilTheLimit()
    {
        // Act
        List<AbsenceCountRow> counts = await _repository.GetAbsenceCounts(September, Day(2026, 9, 10).AddHours(12));

        // Assert
        Assert.Equal([(20, "Math", 1), (21, "Math", 2)],
            counts.Select(c => (c.StudentContractId, c.CourseName, c.Count)).OrderBy(c => c.StudentContractId));
    }

    // r17: absences from the rows of the students' active group rows (the lesson row's group row, of the same
    // contract), not cancelled, before the limit; the last presence (status 1) before the limit; the contacts
    [Fact]
    public async Task GetMissingsInRow_LoadsTheAbsencesTheLastPresencesAndTheContacts()
    {
        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 9, 30), October);

        // Assert
        Assert.Equal([(20, September), (21, September), (21, Day(2026, 9, 10).AddHours(10)),
                (21, Day(2026, 9, 30).AddHours(23))],
            data.Absences.Select(a => (a.StudentContractId, a.LessonDt)).Order());
        Assert.Equal(new Dictionary<int, DateTime> { [20] = Day(2026, 9, 10).AddHours(10) }, data.LastPresences);
        Assert.Equal(new Dictionary<int, StudentContact>
        {
            [20] = new(new SchedulePerson("Gamma", "Cid", "6.020"), "555000003", "Delta Eve", "555000005"),
            [21] = new(new SchedulePerson("Beta", "Dan", "6.021"), "555000004", "Beta Dan", "555000004")
        }, data.Students);
    }

    // a limit before the presence: neither the presence nor the later absences count
    [Fact]
    public async Task GetMissingsInRow_TheLimitCutsPresencesAndAbsences()
    {
        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 9, 30), Day(2026, 9, 10).AddHours(10));

        // Assert
        Assert.Equal([(20, September), (21, September)],
            data.Absences.Select(a => (a.StudentContractId, a.LessonDt)).Order());
        Assert.Empty(data.LastPresences);
    }

    // a group row that ended, or a lesson row of another contract's group row, or a group that is not active on
    // the date: its absences do not count
    [Fact]
    public async Task GetMissingsInRow_OnlyTheActiveGroupRowsOfTheContract()
    {
        // Arrange: contract 20's row in B1 ended on September 20th; a lesson row of 21 points to 20's row in A1;
        // group 102 has no teacher
        Save(GroupStudent(4, 101, 20, September, Day(2026, 9, 20)), LessonStudent(8, 3, 20, 4, false),
            LessonStudent(9, 1, 21, 1, false), new Group { GrpId = 102, GroupCode = "C1", CourseId = 3 },
            GroupStudent(5, 102, 20, September, null), Schedule(3, 102),
            Lesson(6, 102, 5, Day(2026, 9, 15), 1, null, null, September, September),
            LessonStudent(10, 6, 20, 5, false));

        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 9, 30), October);

        // Assert
        Assert.Equal([20, 21, 21, 21], data.Absences.Select(a => a.StudentContractId).Order());
    }

    // a group row is active on the date when it starts before the next midnight and ends on it or later: a row
    // starting at the next midnight does not count, a row ending then does
    [Fact]
    public async Task GetMissingsInRow_GroupRowsStartingOrEndingAtTheNextMidnight()
    {
        // Arrange: lesson 7 (A1, Sep 20); contract 20's row 6 starts on Oct 1, contract 21's row 7 ends on Oct 1
        Save(GroupStudent(6, 100, 20, October, null), GroupStudent(7, 100, 21, September, October),
            Lesson(7, 100, 5, Day(2026, 9, 20), 1, null, null, September, September),
            LessonStudent(11, 7, 20, 6, false), LessonStudent(12, 7, 21, 7, false));

        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 9, 30), October);

        // Assert
        Assert.Contains(data.Absences, a => a == new StudentAbsence(21, Day(2026, 9, 20)));
        Assert.DoesNotContain(data.Absences, a => a == new StudentAbsence(20, Day(2026, 9, 20)));
    }

    // the last presence is the latest one (Access's Max(LessonDT))
    [Fact]
    public async Task GetMissingsInRow_LastPresenceIsTheLatest()
    {
        // Arrange: contract 20 was present on September 10th (lesson 1) and on September 5th
        Save(Lesson(8, 100, 5, Day(2026, 9, 5), 1, null, null, September, September),
            LessonStudent(13, 8, 20, 1, true));

        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 9, 30), October);

        // Assert
        Assert.Equal(Day(2026, 9, 10).AddHours(10), data.LastPresences[20]);
    }

    [Fact]
    public async Task GetMissingsInRow_NoAbsences_IsEmpty()
    {
        // Act
        MissingsInRowData data = await _repository.GetMissingsInRow(Day(2026, 8, 1), Day(2026, 8, 2));

        // Assert
        Assert.Empty(data.Absences);
        Assert.Empty(data.LastPresences);
        Assert.Empty(data.Students);
    }
}
