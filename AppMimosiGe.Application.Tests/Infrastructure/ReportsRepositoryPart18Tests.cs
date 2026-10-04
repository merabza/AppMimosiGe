using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

// the part 18 reports' data: the groups of a date, a month's lessons with comments, the desperate debts, the salary
// details and the work time
public sealed class ReportsRepositoryPart18Tests : IDisposable
{
    //the report date: rows that start on it are active, rows that end on it are not
    private static readonly DateTime Today = Day(2026, 10, 2);
    private static readonly DateTime Tomorrow = Day(2026, 10, 3);
    private static readonly DateTime September = Day(2026, 9, 1);
    private static readonly DateTime October = Day(2026, 10, 1);

    private static readonly SchedulePerson Teacher5 = new("Zeta", "Ann", "T5");
    private static readonly SchedulePerson Teacher6 = new("Alpha", "Bob", "T6");

    private readonly MimosiGeDbContext _context;
    private readonly ReportsRepository _repository;

    public ReportsRepositoryPart18Tests()
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

    //people 1–5 (6 has a legal name), teacher contracts 5 (person 1) and 6 (person 6), student contracts 20 (student
    //3, payer 5), 21 and 22 (students 4 and 2, their own payers); courses Math and Art; sizes Two (2) and Four (4);
    //statuses S1, S2; schemes Fixed, Hours. Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(Human(1, "Zeta", "Ann"), Human(2, "Alpha", "Cid"), Human(3, "Gamma", "Dan"),
            Human(4, "Beta", "Eve"), Human(5, "Delta", "Fay"), Human(6, "Alpha", "Bob", "Robert"));
        _context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T5", TeacherHumanId = 1, RsCountryId = 1 },
            new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 6, RsCountryId = 1 });
        _context.StudentContracts.AddRange(StudentContract(20, "6.020", 3, 5), StudentContract(21, "6.021", 4, 4),
            StudentContract(22, "6.022", 2, 2));
        _context.Courses.AddRange(new Course { CrsId = 3, CourseName = "Math" },
            new Course { CrsId = 4, CourseName = "Art" });
        _context.GroupSizes.AddRange(new GroupSize { GrsId = 1, GrsSize = 2, GrsName = "Two" },
            new GroupSize { GrsId = 2, GrsSize = 4, GrsName = "Four" });
        _context.StudentStatuses.AddRange(new StudentStatus { Id = 1, StudentStatusName = "S1", Rate = 1 },
            new StudentStatus { Id = 2, StudentStatusName = "S2", Rate = 2 });
        _context.TeacherSalarySchemes.AddRange(new TeacherSalaryScheme { Id = 1, SchemaName = "Fixed" },
            new TeacherSalaryScheme { Id = 2, SchemaName = "Hours" });
        _context.LessonStatuses.AddRange(new LessonStatus { Id = 1, StatusName = "held" },
            new LessonStatus { Id = 2, StatusName = "cancelled" }, new LessonStatus { Id = 3, StatusName = "other" });
        _context.SaveChanges();
    }

    private void Save(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private static Human Human(int id, string lastName, string firstName, string? legalName = null)
    {
        return new Human
        {
            HumId = id, LastName = lastName, FirstName = firstName, LegalName = legalName,
            PersonalId = $"0100000000{id}"
        };
    }

    private static StudentContract StudentContract(int id, string number, int studentId, int payerId)
    {
        return new StudentContract
        {
            ScId = id, ContractNumber = number, StudentHumanId = studentId, PayerHumanId = payerId, AcademicYearId = 1
        };
    }

    private static Group Group(int id, string code, int courseId, int sizeId, int statusId,
        DateTime? voidDate = null)
    {
        return new Group
        {
            GrpId = id, GroupCode = code, CourseId = courseId, GroupSizeId = sizeId, StudentStatusId = statusId,
            VoidDate = voidDate
        };
    }

    private static GroupByStudent GroupStudent(int id, int groupId, int contractId, DateTime start, DateTime? end)
    {
        return new GroupByStudent
        {
            GbsId = id, GroupId = groupId, StudentContractId = contractId, StartDate = start, EndDate = end,
            FourWeekHours = 6f + id, FourWeekFee = 40m + id, HoursCoefficient = id / 2f
        };
    }

    private static GroupByTeacher GroupTeacher(int id, int groupId, int contractId, int schemeId, DateTime start,
        DateTime? end)
    {
        return new GroupByTeacher
        {
            Id = id, GroupId = groupId, TeacherContractId = contractId, SalarySchemaId = schemeId, StartDate = start,
            EndDate = end
        };
    }

    private static GroupDayTimePlace Schedule(int id, int groupId)
    {
        return new GroupDayTimePlace
        {
            GdtpId = id, GroupId = groupId, WeekDayId = 1, LessonStartTimeId = 1, RoomId = 1, StartDate = September
        };
    }

    private static Lesson Lesson(int id, int groupId, int teacherId, DateTime lessonDt, int statusId = 1,
        int? substituteId = null, DateTime? recoverDate = null)
    {
        return new Lesson
        {
            Id = id, GroupId = groupId, TeacherContractId = teacherId, SubstituteTeacherContractId = substituteId,
            LessonDt = lessonDt, LessonStatusId = statusId, RecoverDate = recoverDate, TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt, FourWeekHours = 8
        };
    }

    private static LessonByStudent LessonStudent(int id, int lessonId, int contractId, float hours = 1.5f,
        string? teacherComment = null, string? studentComment = null)
    {
        return new LessonByStudent
        {
            Id = id, LessonId = lessonId, StudentContractId = contractId, HoursCount = hours,
            TeacherComment = teacherComment, StudentComment = studentComment
        };
    }

    // the active groups of the date (a cancelled group and one without a schedule are not) with their size, status
    // and course; the student and teacher rows that are active on the date: a row that starts on it is, one that
    // ends on it is not, one that ends the next day is; the names of those rows' contracts only
    [Fact]
    public async Task GetGroups_LoadsTheActiveGroupsAndRows()
    {
        // Arrange
        Save(Group(100, "A1", 3, 2, 1), Group(101, "B1", 4, 1, 2, Today), Group(102, "C1", 4, 1, 2),
            GroupStudent(1, 100, 20, September, null), GroupStudent(2, 100, 21, September, Today),
            GroupStudent(3, 100, 22, Today, null), GroupStudent(4, 100, 21, Tomorrow, null),
            GroupStudent(5, 101, 21, September, null), GroupStudent(6, 102, 21, September, null),
            GroupStudent(7, 100, 22, September, Tomorrow),
            GroupTeacher(1, 100, 5, 1, September, null), GroupTeacher(2, 100, 6, 2, September, Tomorrow),
            GroupTeacher(3, 100, 6, 1, Tomorrow, null), GroupTeacher(4, 101, 6, 1, September, null),
            GroupTeacher(5, 102, 5, 1, September, null), GroupTeacher(6, 100, 5, 2, September, Today),
            Schedule(1, 100), Schedule(2, 101));

        // Act
        GroupsSnapshot snapshot = await _repository.GetGroups(Today);

        // Assert
        Assert.Equal([new ReportGroup(100, "A1", 3, "Math", 2, "Four", 4, 1, "S1")], snapshot.Groups);
        Assert.Equal([
            new ReportGroupStudent(1, 100, 20, 7f, 41m, 0.5f, September),
            new ReportGroupStudent(3, 100, 22, 9f, 43m, 1.5f, Today),
            new ReportGroupStudent(7, 100, 22, 13f, 47m, 3.5f, September)
        ], snapshot.Students.OrderBy(s => s.GbsId));
        Assert.Equal([
            new ReportGroupTeacher(1, 100, 5, "Fixed", September),
            new ReportGroupTeacher(2, 100, 6, "Hours", September)
        ], snapshot.Teachers.OrderBy(t => t.Id));
        Assert.Equal([20, 22], snapshot.StudentNames.Keys.Order());
        Assert.Equal(new SchedulePerson("Gamma", "Dan", "6.020"), snapshot.StudentNames[20]);
        Assert.Equal([5, 6], snapshot.TeacherNames.Keys.Order());
        Assert.Equal(Teacher6, snapshot.TeacherNames[6]);
    }

    [Fact]
    public async Task GetGroups_NoActiveGroup_IsEmpty()
    {
        // Act
        GroupsSnapshot snapshot = await _repository.GetGroups(Today);

        // Assert
        Assert.Empty(snapshot.Groups);
        Assert.Empty(snapshot.Students);
        Assert.Empty(snapshot.Teachers);
    }

    private void SaveCommentLessons()
    {
        Save(Group(100, "A1", 3, 2, 1), Group(101, "B1", 4, 1, 2),
            Lesson(1, 100, 5, September), Lesson(2, 101, 6, October.AddMinutes(-1), 2, 5, Day(2026, 10, 5)),
            Lesson(3, 100, 5, October), Lesson(4, 100, 5, Day(2026, 9, 15)), Lesson(5, 100, 5, September.AddHours(-1)),
            LessonStudent(1, 1, 20, teacherComment: "good", studentComment: "late"), LessonStudent(2, 1, 21),
            LessonStudent(3, 2, 22, studentComment: "sick"), LessonStudent(4, 3, 20), LessonStudent(5, 5, 20));
    }

    // [from, to) lessons with students (a lesson without students is not listed), their students and comments,
    // the substitute and the recovery date
    [Fact]
    public async Task GetCommentLessons_LoadsTheLessonsWithStudents()
    {
        // Arrange
        SaveCommentLessons();

        // Act
        List<CommentLesson> lessons = await _repository.GetCommentLessons(September, October, null);

        // Assert
        Assert.Equal([1, 2], lessons.Select(l => l.LessonId).Order());
        CommentLesson first = lessons.Single(l => l.LessonId == 1);
        Assert.Equal((September, "A1", "Math", Teacher5, (SchedulePerson?)null, (DateTime?)null),
            (first.LessonDt, first.GroupCode, first.CourseName, first.Teacher, first.Substitute, first.RecoverDate));
        Assert.Equal([
            new CommentStudent(20, new SchedulePerson("Gamma", "Dan", "6.020"), "good", "late"),
            new CommentStudent(21, new SchedulePerson("Beta", "Eve", "6.021"), null, null)
        ], first.Students.OrderBy(s => s.StudentContractId));
        CommentLesson second = lessons.Single(l => l.LessonId == 2);
        Assert.Equal(("B1", "Art", Teacher6, Teacher5, Day(2026, 10, 5)),
            (second.GroupCode, second.CourseName, second.Teacher, second.Substitute!, second.RecoverDate!.Value));
        Assert.Equal([new CommentStudent(22, new SchedulePerson("Alpha", "Cid", "6.022"), null, "sick")],
            second.Students);
    }

    // the teacher is the lesson's own teacher, not its substitute
    [Fact]
    public async Task GetCommentLessons_Teacher_OnlyTheTeachersLessons()
    {
        // Arrange
        SaveCommentLessons();

        // Act
        List<CommentLesson> teacher5 = await _repository.GetCommentLessons(September, October, 5);
        List<CommentLesson> teacher6 = await _repository.GetCommentLessons(September, October, 6);

        // Assert
        Assert.Equal([1], teacher5.Select(l => l.LessonId));
        Assert.Equal([20, 21], teacher5[0].Students.Select(s => s.StudentContractId).Order());
        Assert.Equal([2], teacher6.Select(l => l.LessonId));
        Assert.Equal([22], teacher6[0].Students.Select(s => s.StudentContractId));
    }

    // only the payments of an account marked as desperate debt; the people are the contracts' students and payers
    [Fact]
    public async Task GetDesperateDebts_LoadsThePaymentsOfDesperateAccounts()
    {
        // Arrange
        Save(new BankAccount { BaId = 1, BankName = "Bank", BankCode = "B1", AccountNumber = "1" },
            new BankAccount { BaId = 2, BankName = "Debt", BankCode = "D1", AccountNumber = "2", DesperateDebt = true },
            Payment(1, 20, 2, 100m), Payment(2, 21, 1, 30m), Payment(3, 21, null, 40m), Payment(4, 21, 2, -20m));

        // Act
        BlackListData data = await _repository.GetDesperateDebts();

        // Assert
        Assert.Equal([new DebtPayment(1, 3, 5, 100m), new DebtPayment(4, 4, 4, -20m)],
            data.Payments.OrderBy(p => p.PaymentId));
        Assert.Equal([3, 4, 5], data.Humans.Keys.Order());
        Assert.Equal(new Debtor("Gamma", "Dan", "01000000003"), data.Humans[3]);
        return;

        static Payment Payment(int id, int contractId, int? bankId, decimal amount) => new()
        {
            Id = id, StudentContractId = contractId, BankAccountId = bankId, Amount = amount, PayDate = September
        };
    }

    [Fact]
    public async Task GetDesperateDebts_NoPayments_IsEmpty()
    {
        // Act
        BlackListData data = await _repository.GetDesperateDebts();

        // Assert
        Assert.Empty(data.Payments);
        Assert.Empty(data.Humans);
    }

    private void SaveSalaryDetails()
    {
        Save(Group(100, "A1", 3, 2, 1), Group(101, "B1", 4, 1, 2),
            new SalaryHeader { ShId = 1, ShChargeDate = October, ShTransferDate = October },
            Line(1, 5, Day(2026, 8, 1)), Line(2, 5, September), Line(3, 6, October), Line(4, 6, Day(2026, 11, 1)),
            Detail(1, 1, 100), Detail(2, 2, 100), Detail(3, 3, 101), Detail(4, 4, 100));
        return;

        static SalaryLine Line(int id, int teacherId, DateTime month) => new()
        {
            SaId = id, ShId = 1, TeacherContractId = teacherId, SaMonthDate = month
        };

        static SalaryLineDetail Detail(int id, int lineId, int groupId) => new()
        {
            SadId = id, SaId = lineId, GroupId = groupId, SadHoursCount = id * 1.5f, SadHourCost = 10m + id,
            SadAmount = 20m * id
        };
    }

    // the details whose line's month is in [from, to] (first days), with the month, the teacher (the legal name
    // if there is one) and the group
    [Fact]
    public async Task GetSalaryDetails_LoadsTheMonthsDetails()
    {
        // Arrange
        SaveSalaryDetails();

        // Act
        List<SalaryDetailRow> details = await _repository.GetSalaryDetails(September, October, null);

        // Assert
        Assert.Equal([
            new SalaryDetailRow(2, September, 5, "Zeta", "Ann", "T5", 100, "A1", "Math", 3f, 12m, 40m),
            new SalaryDetailRow(3, October, 6, "Alpha", "Robert", "T6", 101, "B1", "Art", 4.5f, 13m, 60m)
        ], details.OrderBy(d => d.SadId));
    }

    [Fact]
    public async Task GetSalaryDetails_Teacher_OnlyTheTeachersDetails()
    {
        // Arrange
        SaveSalaryDetails();

        // Act & Assert
        Assert.Equal([3], (await _repository.GetSalaryDetails(September, October, 6)).Select(d => d.SadId));
        Assert.Equal([2], (await _repository.GetSalaryDetails(September, October, 5)).Select(d => d.SadId));
    }

    [Fact]
    public async Task GetMonthNames_AreTheGeorgianMonths()
    {
        // Arrange
        Save(new GeoMonth { GmnId = 1, GmnName = "იანვარი", GmnDative = "იანვარს" },
            new GeoMonth { GmnId = 9, GmnName = "სექტემბერი", GmnDative = "სექტემბერს" });

        // Act
        IReadOnlyDictionary<int, string> names = await _repository.GetMonthNames();

        // Assert
        Assert.Equal([(1, "იანვარი"), (9, "სექტემბერი")], names.OrderBy(n => n.Key).Select(n => (n.Key, n.Value)));
    }

    // lessons of [from, to) by the day they were held (the recovery date if there is one), not cancelled, with
    // students: the employee is the substitute if there is one, the hours the students' maximum. Work records that
    // start and end in [from, to), finished ones only
    [Fact]
    public async Task GetWorkTime_LoadsTheLessonsAndTheRecords()
    {
        // Arrange
        Save(Group(100, "A1", 3, 2, 1),
            Lesson(1, 100, 5, September), Lesson(2, 100, 5, Day(2026, 9, 30).AddHours(23), 2),
            Lesson(3, 100, 5, Day(2026, 9, 10), 3, 6), Lesson(4, 100, 5, October),
            Lesson(5, 100, 5, Day(2026, 8, 31).AddHours(10), recoverDate: Day(2026, 9, 2)),
            Lesson(6, 100, 5, Day(2026, 9, 29).AddHours(10), recoverDate: October), Lesson(7, 100, 5, Day(2026, 9, 15)),
            LessonStudent(1, 1, 20, 1.5f), LessonStudent(2, 1, 21, 2f), LessonStudent(3, 2, 20),
            LessonStudent(4, 3, 20, 1f), LessonStudent(5, 4, 20), LessonStudent(6, 5, 20, 2f), LessonStudent(7, 6, 20),
            Record(1, 5, September, September.AddHours(8)), Record(2, 6, September.AddHours(-1), September.AddHours(1)),
            Record(3, 5, Day(2026, 9, 30).AddHours(22), October), Record(4, 5, Day(2026, 9, 30).AddHours(22),
                October.AddMinutes(-1)), Record(5, 6, Day(2026, 9, 5), null));

        // Act
        WorkTimeData data = await _repository.GetWorkTime(September, October);

        // Assert
        Assert.Equal([
            new WorkTimeLesson(1, 5, September, null, 2f), new WorkTimeLesson(3, 6, Day(2026, 9, 10), null, 1f),
            new WorkTimeLesson(5, 5, Day(2026, 8, 31).AddHours(10), Day(2026, 9, 2), 2f)
        ], data.Lessons.OrderBy(l => l.LessonId));
        Assert.Equal([
            new WorkTimeRecord(1, 5, September, September.AddHours(8)),
            new WorkTimeRecord(4, 5, Day(2026, 9, 30).AddHours(22), October.AddMinutes(-1))
        ], data.Records.OrderBy(r => r.WhId));
        Assert.Equal([5, 6], data.Employees.Keys.Order());
        Assert.Equal(Teacher5, data.Employees[5]);
        return;

        static WorkHour Record(int id, int contractId, DateTime start, DateTime? end) => new()
        {
            WhId = id, TeacherContractId = contractId, WhStart = start, WhEnd = end
        };
    }

    [Fact]
    public async Task GetWorkTime_EmptyPeriod_IsEmpty()
    {
        // Act
        WorkTimeData data = await _repository.GetWorkTime(September, October);

        // Assert
        Assert.Empty(data.Lessons);
        Assert.Empty(data.Records);
        Assert.Empty(data.Employees);
    }
}
