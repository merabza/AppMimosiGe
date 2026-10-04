using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class ReportsRepositoryTests : IDisposable
{
    //the report date: rows that start on it are active, rows that end on it are not
    private static readonly DateTime Today = Day(2026, 10, 2);
    private static readonly DateTime Tomorrow = Day(2026, 10, 3);
    private static readonly DateTime SeptemberFirst = Day(2026, 9, 1);

    private readonly MimosiGeDbContext _context;
    private readonly ReportsRepository _repository;

    public ReportsRepositoryTests()
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

    //groups: 100 A1 active (with an ended student row, a future teacher row and an ended schedule row),
    //101 B1 cancelled today, 102 C1 cancelled tomorrow, 103 D1 its only student left today, 104 E1 its teacher
    //starts tomorrow, 105 F1 its schedule starts today and its student leaves tomorrow, 106 G1 no schedule.
    //Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(Human(1, "Zeta", "Ann"), Human(2, "Alpha", "Bob"), Human(3, "Gamma", "Cid"),
            Human(4, "Beta", "Dan"));
        _context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T5", TeacherHumanId = 1, RsCountryId = 1 },
            new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 2, RsCountryId = 1 },
            new TeacherContract { Id = 7, ContractNumber = "T7", TeacherHumanId = 2, RsCountryId = 1 });
        _context.StudentContracts.AddRange(StudentContract(20, "6.020", 3), StudentContract(21, "6.021", 4),
            StudentContract(22, "6.022", 4));
        _context.Courses.AddRange(new Course { CrsId = 3, CourseName = "Math" },
            new Course { CrsId = 4, CourseName = "Art" }, new Course { CrsId = 5, CourseName = "Art" });
        _context.Rooms.AddRange(new Room { Id = 1, RoomName = "R1" }, new Room { Id = 2, RoomName = "R2" });
        _context.WeekDays.AddRange(new WeekDay { Id = 10, WeekDayNumber = 2, ShortName = "2-სამ", Name = "Tue" },
            new WeekDay { Id = 11, WeekDayNumber = 1, ShortName = "1-ორ", Name = "Mon" });
        _context.LessonStartTimes.AddRange(new LessonStartTime { LstId = 1, LstTime = new TimeOnly(10, 0) },
            new LessonStartTime { LstId = 2, LstTime = new TimeOnly(15, 30) });

        _context.Groups.AddRange(Group(100, "A1", 3), Group(101, "B1", 4, Today), Group(102, "C1", 4, Tomorrow),
            Group(103, "D1", 3), Group(104, "E1", 3), Group(105, "F1", 4), Group(106, "G1", 3));
        _context.GroupsByStudents.AddRange(Student(1, 100, 20, SeptemberFirst, null),
            Student(2, 100, 21, SeptemberFirst, Today), Student(3, 101, 21, SeptemberFirst, null),
            Student(4, 102, 21, SeptemberFirst, null), Student(5, 103, 22, SeptemberFirst, Today),
            Student(6, 104, 22, SeptemberFirst, null), Student(7, 105, 22, Today, Tomorrow),
            Student(8, 106, 22, SeptemberFirst, null));
        _context.GroupsByTeachers.AddRange(Teacher(1, 100, 5, SeptemberFirst, null), Teacher(2, 100, 6, Tomorrow, null),
            Teacher(3, 101, 6, SeptemberFirst, null), Teacher(4, 102, 6, SeptemberFirst, Tomorrow),
            Teacher(5, 103, 6, SeptemberFirst, null), Teacher(6, 104, 6, Tomorrow, null),
            Teacher(7, 105, 7, SeptemberFirst, null), Teacher(8, 106, 7, SeptemberFirst, null));
        _context.GroupDayTimePlaces.AddRange(Lesson(1, 100, 11, 1, 1.5f, 1, SeptemberFirst, null),
            Lesson(2, 100, 10, 2, 2f, 2, SeptemberFirst, Today), Lesson(3, 101, 11, 1, 1f, 1, SeptemberFirst, null),
            Lesson(4, 102, 10, 2, 1f, 2, SeptemberFirst, null), Lesson(5, 103, 11, 1, 1f, 1, SeptemberFirst, null),
            Lesson(6, 104, 11, 1, 1f, 1, SeptemberFirst, null), Lesson(7, 105, 10, 1, 2f, 1, Today, null));
        _context.SaveChanges();
    }

    //a test's own rows next to the common ones; saved synchronously like the seed
    private void Save(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
    }

    //a group with one student, one teacher and one schedule row, active from September except the given dates
    private static object[] GroupWithRows(int id, DateTime? studentStart = null, DateTime? lessonStart = null,
        DateTime? lessonEnd = null)
    {
        return
        [
            Group(id, $"N{id}", 3), Student(id, id, 22, studentStart ?? SeptemberFirst, null),
            Teacher(id, id, 7, SeptemberFirst, null),
            Lesson(id, id, 11, 1, 1f, 1, lessonStart ?? SeptemberFirst, lessonEnd)
        ];
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };
    }

    private static StudentContract StudentContract(int id, string number, int humanId)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = humanId,
            PayerHumanId = humanId,
            AcademicYearId = 1
        };
    }

    private static Group Group(int id, string code, int courseId, DateTime? voidDate = null)
    {
        return new Group { GrpId = id, GroupCode = code, CourseId = courseId, VoidDate = voidDate };
    }

    private static GroupByStudent Student(int id, int groupId, int contractId, DateTime start, DateTime? end)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = groupId,
            StudentContractId = contractId,
            StartDate = start,
            EndDate = end
        };
    }

    private static GroupByTeacher Teacher(int id, int groupId, int contractId, DateTime start, DateTime? end)
    {
        return new GroupByTeacher
        {
            Id = id,
            GroupId = groupId,
            TeacherContractId = contractId,
            StartDate = start,
            EndDate = end
        };
    }

    private static GroupDayTimePlace Lesson(int id, int groupId, int weekDayId, int startTimeId, float hours,
        int roomId, DateTime start, DateTime? end)
    {
        return new GroupDayTimePlace
        {
            GdtpId = id,
            GroupId = groupId,
            WeekDayId = weekDayId,
            LessonStartTimeId = startTimeId,
            HoursCount = hours,
            RoomId = roomId,
            StartDate = start,
            EndDate = end
        };
    }

    // Access's vActiveGroupsForReports: not cancelled, and an active student, teacher and schedule row on the day
    [Fact]
    public async Task ActiveGroupsForReports_ReturnsTheGroupsActiveOnTheDay()
    {
        // Act
        List<int> ids = await ActiveGroupsForReports.Query(_context, Today).Select(g => g.GrpId).OrderBy(id => id)
            .ToListAsync();

        // Assert
        Assert.Equal([100, 102, 105], ids);
    }

    // the time of the date does not matter: the state is for the whole day
    [Fact]
    public async Task ActiveGroupsForReports_TimeOfTheDate_DoesNotMatter()
    {
        // Act
        List<int> ids = await ActiveGroupsForReports.Query(_context, Today.AddHours(23).AddMinutes(59))
            .Select(g => g.GrpId).OrderBy(id => id).ToListAsync();

        // Assert
        Assert.Equal([100, 102, 105], ids);
    }

    // the day before every row ends or starts: the rows ending today are still active, the ones starting today not
    [Fact]
    public async Task ActiveGroupsForReports_DayBefore()
    {
        // Act
        List<int> ids = await ActiveGroupsForReports.Query(_context, Today.AddDays(-1)).Select(g => g.GrpId)
            .OrderBy(id => id).ToListAsync();

        // Assert
        Assert.Equal([100, 101, 102, 103], ids);
    }

    [Fact]
    public void ActiveGroupsForReports_DayAfter_IsTheNextMidnight()
    {
        Assert.Equal(Tomorrow, ActiveGroupsForReports.DayAfter(Today.AddHours(15.5)));
    }

    // a student or schedule row that starts the next day is not active yet, a schedule row that ends then still is
    [Fact]
    public async Task ActiveGroupsForReports_RowsStartingOrEndingTheNextDay()
    {
        // Arrange: 107 its student starts tomorrow, 108 its schedule starts tomorrow, 109 its schedule ends
        // tomorrow, 110 its only schedule row ended today
        Save([
            .. GroupWithRows(107, Tomorrow), .. GroupWithRows(108, lessonStart: Tomorrow),
            .. GroupWithRows(109, lessonEnd: Tomorrow), .. GroupWithRows(110, lessonEnd: Today)
        ]);

        // Act
        List<int> ids = await ActiveGroupsForReports.Query(_context, Today).Where(g => g.GrpId >= 107)
            .Select(g => g.GrpId).OrderBy(id => id).ToListAsync();

        // Assert
        Assert.Equal([109], ids);
    }

    // only the active groups and their rows that are active on the day
    [Fact]
    public async Task GetSchedule_LoadsTheActiveGroupsAndTheirRowsOfTheDay()
    {
        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Today);

        // Assert
        Assert.Equal([
            new ScheduleGroup(100, "A1", 3, "Math"), new ScheduleGroup(102, "C1", 4, "Art"),
            new ScheduleGroup(105, "F1", 4, "Art")
        ], schedule.Groups.OrderBy(g => g.GroupId));
        Assert.Equal([
            new ScheduleStudentRow(1, 100, 20), new ScheduleStudentRow(4, 102, 21),
            new ScheduleStudentRow(7, 105, 22)
        ], schedule.Students.OrderBy(s => s.GbsId));
        Assert.Equal([
            new ScheduleTeacherRow(1, 100, 5), new ScheduleTeacherRow(4, 102, 6),
            new ScheduleTeacherRow(7, 105, 7)
        ], schedule.Teachers.OrderBy(t => t.GbtId));
        Assert.Equal([
            new ScheduleLessonRow(1, 100, 11, new TimeOnly(10, 0), 1.5f, 1),
            new ScheduleLessonRow(4, 102, 10, new TimeOnly(15, 30), 1f, 2),
            new ScheduleLessonRow(7, 105, 10, new TimeOnly(10, 0), 2f, 1)
        ], schedule.Lessons.OrderBy(l => l.GdtpId));
    }

    // in an active group: a student or schedule row starting tomorrow is not loaded, a schedule row ending then is
    [Fact]
    public async Task GetSchedule_RowsStartingOrEndingTheNextDay()
    {
        // Arrange
        Save(Student(9, 100, 21, Tomorrow, null), Lesson(8, 100, 11, 2, 1f, 1, Tomorrow, null),
            Lesson(9, 100, 10, 1, 1f, 2, SeptemberFirst, Tomorrow));

        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Today);

        // Assert
        Assert.Equal([1], schedule.Students.Where(s => s.GroupId == 100).Select(s => s.GbsId));
        Assert.Equal([1, 9], schedule.Lessons.Where(l => l.GroupId == 100).Select(l => l.GdtpId).OrderBy(id => id));
    }

    // the week days in the order of their numbers (the crosstab's fixed column order), the rooms by id
    [Fact]
    public async Task GetSchedule_LoadsTheWeekDaysInNumberOrderAndTheRooms()
    {
        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Today);

        // Assert
        Assert.Equal([new ScheduleWeekDay(11, "1-ორ"), new ScheduleWeekDay(10, "2-სამ")], schedule.WeekDays);
        Assert.Equal(new Dictionary<int, string> { [1] = "R1", [2] = "R2" }, schedule.RoomNames);
    }

    // week days with one number (Access's default 0) are in the order of their ids
    [Fact]
    public async Task GetSchedule_WeekDaysWithOneNumber_AreInIdOrder()
    {
        // Arrange
        Save(new WeekDay { Id = 9, WeekDayNumber = 1, ShortName = "1-ორ2", Name = "Mon2" });

        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Today);

        // Assert
        Assert.Equal([9, 11, 10], schedule.WeekDays.Select(w => w.WeekDayId));
    }

    // names only of the contracts of the loaded rows
    [Fact]
    public async Task GetSchedule_LoadsTheNamesOfTheRowsContracts()
    {
        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Today);

        // Assert
        Assert.Equal(
            new Dictionary<int, SchedulePerson>
            {
                [5] = new("Zeta", "Ann", "T5"), [6] = new("Alpha", "Bob", "T6"), [7] = new("Alpha", "Bob", "T7")
            }, schedule.TeacherNames);
        Assert.Equal(
            new Dictionary<int, SchedulePerson>
            {
                [20] = new("Gamma", "Cid", "6.020"),
                [21] = new("Beta", "Dan", "6.021"),
                [22] = new("Beta", "Dan", "6.022")
            }, schedule.StudentNames);
    }

    [Fact]
    public async Task GetSchedule_DayWithoutActiveGroups_IsEmpty()
    {
        // Act
        ScheduleSnapshot schedule = await _repository.GetSchedule(Day(2026, 8, 1));

        // Assert
        Assert.Empty(schedule.Groups);
        Assert.Empty(schedule.Students);
        Assert.Empty(schedule.Teachers);
        Assert.Empty(schedule.Lessons);
        Assert.Empty(schedule.TeacherNames);
        Assert.Empty(schedule.StudentNames);
        Assert.Equal(2, schedule.WeekDays.Count);
    }

    // Access's combos: "last first / number" by name and then id, courses by name and then id
    [Fact]
    public async Task GetLookups_ReturnsTheListsInNameOrder()
    {
        // Act
        ReportLookupsResponse lookups = await _repository.GetLookups();

        // Assert
        Assert.Equal([
            new LookupItemResponse(6, "Alpha Bob / T6"), new LookupItemResponse(7, "Alpha Bob / T7"),
            new LookupItemResponse(5, "Zeta Ann / T5")
        ], lookups.Teachers);
        Assert.Equal([
            new LookupItemResponse(4, "Art"), new LookupItemResponse(5, "Art"), new LookupItemResponse(3, "Math")
        ], lookups.Courses);
        Assert.Equal([
            new LookupItemResponse(21, "Beta Dan / 6.021"), new LookupItemResponse(22, "Beta Dan / 6.022"),
            new LookupItemResponse(20, "Gamma Cid / 6.020")
        ], lookups.Students);
    }

    // a student's contract number is unique only in its year: one name twice is in the order of the ids
    [Fact]
    public async Task GetLookups_StudentsWithOneName_AreInIdOrder()
    {
        // Arrange
        StudentContract nextYear = StudentContract(19, "6.020", 3);
        nextYear.AcademicYearId = 2;
        Save(nextYear);

        // Act
        ReportLookupsResponse lookups = await _repository.GetLookups();

        // Assert
        Assert.Equal([21, 22, 19, 20], lookups.Students.Select(s => s.Id));
    }
}
