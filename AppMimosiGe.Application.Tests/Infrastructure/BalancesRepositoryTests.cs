using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class BalancesRepositoryTests : IDisposable
{
    private static readonly DateTime Today = At(10, 1);

    private readonly MimosiGeDbContext _context;
    private readonly BalancesRepository _repository;

    public BalancesRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new BalancesRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime At(int month, int day, int hour = 0) =>
        new(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);

    //contracts 10 (Alpha Ann, payer Beta Bob, day 15, dirty) and 11 (Gamma Gia pays herself) of 2026-2027, 12 (Delta
    //Dan, payer Beta Bob, dirty) of 2025-2026. Group 100 English with rows 200 (contract 10) and 202 (11); group 101
    //Math, void on 20.12, with row 201 (10, ended 01.11). Lessons: 300 (100, 03.09 15:00), 301 (100, 10.09, cancelled),
    //302 (101, 05.10 16:00), 303 (100, 08.10 15:00), 304 (100, 02.10 15:00, cancelled), 305 (100, today 09:00).
    //Synthetic people only: never real names
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.Humans.AddRange(Human(1, "Alpha", "Ann", "555000001"), Human(2, "Beta", "Bob", "555000002"),
            Human(3, "Gamma", "Gia", null), Human(4, "Delta", "Dan", "555000004"));
        _context.StudentContracts.AddRange(
            new StudentContract
            {
                ScId = 10,
                ContractNumber = "6.001",
                StudentHumanId = 1,
                PayerHumanId = 2,
                AcademicYearId = 11,
                DesiredMonthlyPaymentDay = 15,
                NextPayDate = At(9, 3, 15),
                DirtyNextPayDate = true
            }, new StudentContract
            {
                ScId = 11,
                ContractNumber = "6.002",
                StudentHumanId = 3,
                PayerHumanId = 3,
                AcademicYearId = 11,
                DirtyNextPayDate = false
            }, new StudentContract
            {
                ScId = 12,
                ContractNumber = "5.001",
                StudentHumanId = 4,
                PayerHumanId = 2,
                AcademicYearId = 10,
                DirtyNextPayDate = true
            });
        _context.Courses.AddRange(new Course { CrsId = 1, CourseName = "English" },
            new Course { CrsId = 2, CourseName = "Math" });
        _context.Groups.AddRange(new Group { GrpId = 100, GroupCode = "E1", CourseId = 1, AcademicYearId = 11 },
            new Group
            {
                GrpId = 101,
                GroupCode = "M1",
                CourseId = 2,
                AcademicYearId = 11,
                VoidDate = At(12, 20)
            });
        _context.GroupsByStudents.AddRange(GroupStudent(200, 100, 10, 48m, 8f, null),
            GroupStudent(201, 101, 10, 100m, 12f, At(11, 1)), GroupStudent(202, 100, 11, 60m, 6f, null));
        _context.Lessons.AddRange(Lesson(300, 100, At(9, 3, 15)), Lesson(301, 100, At(9, 10, 15), 2),
            Lesson(302, 101, At(10, 5, 16)), Lesson(303, 100, At(10, 8, 15)), Lesson(304, 100, At(10, 2, 15), 2),
            Lesson(305, 100, At(10, 1, 9)));
        _context.LessonsByStudents.AddRange(LessonStudent(400, 300, 10, 200, 1f), //a charge
            LessonStudent(401, 301, 10, 200, 1f), //cancelled lesson: no charge
            LessonStudent(402, 302, 10, 201, 2f), //a charge of Math
            LessonStudent(403, 300, 11, 202, 1.5f), //a charge of contract 11
            LessonStudent(404, 300, 11, 200, 1f), //the row of another contract: no charge
            LessonStudent(405, 302, 11, 202, 1f), //the row of another group: no charge
            LessonStudent(406, 303, 10, null, 1f), //no row: no charge
            LessonStudent(407, 304, 10, 200, 1f), //cancelled lesson: no charge
            LessonStudent(408, 305, 11, 202, 1f)); //a charge of today
        _context.Payments.AddRange(Payment(500, 10, At(9, 1), 100m, "d1"), Payment(501, 11, At(9, 2), 50m, null),
            Payment(502, 12, At(8, 31), 20m, "old"));
        _context.CrmCalls.AddRange(Call(600, 10, At(9, 20, 10), At(9, 25)), Call(601, 10, At(9, 22, 10), null),
            Call(602, 12, At(9, 21, 10), At(9, 30)), Call(603, 11, At(9, 23, 10), null));
        _context.OperationMonths.AddRange(new OperationMonth { Id = 1, MonthDate = At(9, 1) },
            new OperationMonth { Id = 2, MonthDate = At(11, 1).AddYears(1) });
        _context.SaveChanges();
    }

    //sync EF calls stay out of the async tests
    private void ClearOperationMonths()
    {
        _context.OperationMonths.RemoveRange(_context.OperationMonths);
        _context.SaveChanges();
    }

    private void AddLessonOfContract(int lessonId, int lessonStudentId, int scId, DateTime lessonDt)
    {
        _context.Lessons.Add(Lesson(lessonId, 100, lessonDt));
        _context.LessonsByStudents.Add(LessonStudent(lessonStudentId, lessonId, scId, null, 1f));
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    private DateTime? StoredNextPayDate(int scId)
    {
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return _context.StudentContracts.Single(c => c.ScId == scId).NextPayDate;
    }

    private static Human Human(int id, string lastName, string firstName, string? phoneNumber)
    {
        return new Human
        {
            HumId = id,
            LastName = lastName,
            FirstName = firstName,
            PersonalId = $"0100000000{id}",
            PhoneNumber = phoneNumber
        };
    }

    private static GroupByStudent GroupStudent(int id, int grpId, int scId, decimal fee, float hours, DateTime? endDate)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = grpId,
            StudentContractId = scId,
            FourWeekFee = fee,
            FourWeekHours = hours,
            StartDate = At(9, 1),
            EndDate = endDate
        };
    }

    private static Lesson Lesson(int id, int grpId, DateTime lessonDt, int statusId = 1)
    {
        return new Lesson { Id = id, GroupId = grpId, LessonDt = lessonDt, LessonStatusId = statusId };
    }

    private static LessonByStudent LessonStudent(int id, int lessonId, int scId, int? gbsId, float hours)
    {
        return new LessonByStudent
        {
            Id = id,
            LessonId = lessonId,
            StudentContractId = scId,
            GroupByStudentId = gbsId,
            HoursCount = hours
        };
    }

    private static Payment Payment(int id, int scId, DateTime payDate, decimal amount, string? document)
    {
        return new Payment
        {
            Id = id,
            StudentContractId = scId,
            PayDate = payDate,
            Amount = amount,
            Document = document
        };
    }

    private static CrmCall Call(int id, int scId, DateTime callDate, DateTime? mustPayDate)
    {
        return new CrmCall { CcId = id, StudentContractId = scId, CallDate = callDate, MustPayDate = mustPayDate };
    }

    [Fact]
    public async Task GetCharges_All_TakesTheRowsOfTheirOwnContractAndGroupWithoutCancelledLessons()
    {
        // Act
        List<ChargeData> charges = await _repository.GetCharges(null);

        // Assert
        Assert.Equal([
            new ChargeData(400, 10, At(9, 3, 15), "English", 48m, 8f, 1f),
            new ChargeData(402, 10, At(10, 5, 16), "Math", 100m, 12f, 2f),
            new ChargeData(403, 11, At(9, 3, 15), "English", 60m, 6f, 1.5f),
            new ChargeData(408, 11, At(10, 1, 9), "English", 60m, 6f, 1f)
        ], charges.OrderBy(c => c.LessonByStudentId));
    }

    [Fact]
    public async Task GetCharges_OfContracts_TakesOnlyTheirs()
    {
        // Act
        List<ChargeData> charges = await _repository.GetCharges([10, 12]);

        // Assert
        Assert.Equal([400, 402], charges.Select(c => c.LessonByStudentId).Order());
    }

    [Fact]
    public async Task GetPayments_AllOrOfContracts()
    {
        // Act
        List<PaymentData> all = await _repository.GetPayments(null);
        List<PaymentData> some = await _repository.GetPayments([10, 12]);

        // Assert
        Assert.Equal([500, 501, 502], all.Select(p => p.Id).Order());
        Assert.Equal([
            new PaymentData(500, 10, At(9, 1), "d1", 100m),
            new PaymentData(502, 12, At(8, 31), "old", 20m)
        ], some.OrderBy(p => p.Id));
    }

    [Fact]
    public async Task GetStudentContractNames_AreStudentAndNumber()
    {
        // Act
        Dictionary<int, string> names = await _repository.GetStudentContractNames([10, 12]);

        // Assert
        Assert.Equal(new Dictionary<int, string> { [10] = "Alpha Ann / 6.001", [12] = "Delta Dan / 5.001" }, names);
    }

    [Fact]
    public async Task GetDepositContracts_OfTheYear_HaveTheStudentAndThePayer()
    {
        // Act
        List<DepositContractData> contracts = await _repository.GetDepositContracts(11);

        // Assert
        Assert.Equal([
            new DepositContractData(10, 11, "Alpha Ann", "6.001", "555000001", "Beta Bob", "555000002", 15,
                At(9, 3, 15)),
            new DepositContractData(11, 11, "Gamma Gia", "6.002", null, "Gamma Gia", null, null, null)
        ], contracts.OrderBy(c => c.StudentContractId));
    }

    [Fact]
    public async Task GetDepositContracts_NoYear_TakesAll()
    {
        // Act
        List<DepositContractData> contracts = await _repository.GetDepositContracts(null);

        // Assert
        Assert.Equal([10, 11, 12], contracts.Select(c => c.StudentContractId).Order());
    }

    //any row of the contract counts (without the group row check), from today on, without cancelled lessons
    [Fact]
    public async Task GetNextLessonDates_AreTheFirstNotCancelledLessonsFromToday()
    {
        // Act
        Dictionary<int, DateTime> nextLessons = await _repository.GetNextLessonDates([10, 11, 12], Today);

        // Assert: 10: 302 (05.10; 304 of 02.10 is cancelled); 11: 305 (today 09:00); 12 has no lesson
        Assert.Equal(new Dictionary<int, DateTime> { [10] = At(10, 5, 16), [11] = At(10, 1, 9) }, nextLessons);
    }

    [Fact]
    public async Task GetNextLessonDates_OnlyOfTheContracts()
    {
        // Act
        Dictionary<int, DateTime> nextLessons = await _repository.GetNextLessonDates([11], At(10, 2));

        // Assert: after today's lesson, 302 through the row of another group
        Assert.Equal(new Dictionary<int, DateTime> { [11] = At(10, 5, 16) }, nextLessons);
    }

    //"from today" takes a lesson at today's midnight too
    [Fact]
    public async Task GetNextLessonDates_LessonAtTodaysMidnight_Counts()
    {
        // Arrange
        AddLessonOfContract(306, 409, 12, At(10, 3));

        // Act
        Dictionary<int, DateTime> nextLessons = await _repository.GetNextLessonDates([12], At(10, 3));

        // Assert
        Assert.Equal(new Dictionary<int, DateTime> { [12] = At(10, 3) }, nextLessons);
    }

    [Fact]
    public async Task GetCrmMustPayDates_AreTheCallsWithTheDate()
    {
        // Act
        List<CrmMustPayDateData> calls = await _repository.GetCrmMustPayDates([10, 11]);

        // Assert
        Assert.Equal([new CrmMustPayDateData(600, 10, At(9, 20, 10), At(9, 25))], calls);
    }

    [Fact]
    public async Task GetGroupStudents_HaveTheFeeTheEndAndTheGroupVoidDate()
    {
        // Act
        List<DepositGroupStudentData> rows = await _repository.GetGroupStudents([10, 12]);

        // Assert
        Assert.Equal([
            new DepositGroupStudentData(10, 48m, null, null),
            new DepositGroupStudentData(10, 100m, At(11, 1), At(12, 20))
        ], rows.OrderBy(r => r.FourWeekFee));
    }

    [Fact]
    public async Task GetLastOperationMonth_IsTheLatestMonth()
    {
        Assert.Equal(At(11, 1).AddYears(1), await _repository.GetLastOperationMonth());
    }

    [Fact]
    public async Task GetLastOperationMonth_EmptyCalendar_IsNull()
    {
        // Arrange
        ClearOperationMonths();

        // Act + Assert
        Assert.Null(await _repository.GetLastOperationMonth());
    }

    [Fact]
    public async Task GetStudentContractsForRecount_OnlyDirty_TakesTheFlaggedOnesForChange()
    {
        // Act
        List<StudentContract> contracts = await _repository.GetStudentContractsForRecount(true);
        contracts[0].NextPayDate = null;

        // Assert: tracked, so the change is saved
        Assert.Equal([10, 12], contracts.Select(c => c.ScId));
        Assert.Null(StoredNextPayDate(10));
    }

    [Fact]
    public async Task GetStudentContractsForRecount_All_TakesEveryContractById()
    {
        // Act
        List<StudentContract> contracts = await _repository.GetStudentContractsForRecount(false);

        // Assert
        Assert.Equal([10, 11, 12], contracts.Select(c => c.ScId));
    }
}
