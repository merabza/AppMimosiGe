using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

// the check reports' data (part 17): every group row, whatever its dates
public sealed class ReportsRepositoryGroupRowsTests : IDisposable
{
    private static readonly DateTime September = Day(2026, 9, 1);

    private readonly MimosiGeDbContext _context;
    private readonly ReportsRepository _repository;

    public ReportsRepositoryGroupRowsTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
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

    //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set, so test
    //data is saved synchronously. Synthetic people only: never real names
    private void Seed()
    {
        _context.Humans.AddRange(Human(1, "Zeta", "Ann"), Human(2, "Gamma", "Cid"));
        _context.TeacherContracts.AddRange(
            new TeacherContract
            {
                Id = 5, ContractNumber = "T5", TeacherHumanId = 1, RsCountryId = 1, SalarySchemaByHoursId = 7
            }, new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 1, RsCountryId = 1 });
        _context.StudentContracts.AddRange(
            new StudentContract
            {
                ScId = 20, ContractNumber = "6.020", StudentHumanId = 2, PayerHumanId = 2, AcademicYearId = 1
            }, new StudentContract
            {
                ScId = 21, ContractNumber = "6.021", StudentHumanId = 2, PayerHumanId = 2, AcademicYearId = 1
            });
        _context.Courses.AddRange(new Course { CrsId = 3, CourseName = "Math" });
        _context.TeacherSalarySchemes.AddRange(new TeacherSalaryScheme { Id = 7, SchemaName = "S7" },
            new TeacherSalaryScheme { Id = 8, SchemaName = "S8" });
        _context.AcademicYears.AddRange(Year(1, Day(2026, 9, 1)), Year(2, Day(2027, 9, 1)));
        _context.Groups.AddRange(
            new Group { GrpId = 100, GroupCode = "A1", CourseId = 3, VoidDate = Day(2027, 1, 1) },
            new Group { GrpId = 101, GroupCode = "B1", CourseId = 3 });
        _context.GroupsByStudents.AddRange(
            new GroupByStudent
            {
                GbsId = 1, GroupId = 100, StudentContractId = 20, StartDate = September, EndDate = Day(2026, 9, 20),
                FourWeekHours = 12f, FourWeekFee = 72.5m, OneHourFee = 6.0417m, HoursCoefficient = 1.5f
            }, new GroupByStudent { GbsId = 2, GroupId = 101, StudentContractId = 20, StartDate = Day(2025, 9, 1) });
        _context.GroupsByTeachers.AddRange(
            new GroupByTeacher
            {
                Id = 1, GroupId = 100, TeacherContractId = 5, SalarySchemaId = 8, StartDate = September,
                EndDate = Day(2026, 9, 1)
            }, new GroupByTeacher { Id = 2, GroupId = 101, TeacherContractId = 6, SalarySchemaId = 7, StartDate = September });
        _context.GroupDayTimePlaces.AddRange(new GroupDayTimePlace
        {
            GdtpId = 1, GroupId = 100, WeekDayId = 1, LessonStartTimeId = 1, RoomId = 1, HoursCount = 1.5f,
            StartDate = September, EndDate = Day(2026, 12, 1)
        });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
        return;

        static Human Human(int id, string lastName, string firstName) =>
            new() { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };

        static AcademicYear Year(int id, DateTime finishDate) => new()
        {
            AyId = id, AcademicYearName = $"Y{id}", StartDate = finishDate.AddYears(-1), FinishDate = finishDate
        };
    }

    // every group, student, teacher and schedule row with their dates, fees and schemes; names only of the rows'
    // contracts, the teachers' main schemes, the scheme names and the last academic year's finish (vMaxDate)
    [Fact]
    public async Task GetGroupRows_LoadsEveryGroupRow()
    {
        // Arrange
        Seed();

        // Act
        GroupRowsSnapshot snapshot = await _repository.GetGroupRows(null);

        // Assert
        Assert.Equal([new CheckGroup(100, "A1", 3, "Math", Day(2027, 1, 1)), new CheckGroup(101, "B1", 3, "Math", null)],
            snapshot.Groups.OrderBy(g => g.GroupId));
        Assert.Equal([
            new CheckStudentRow(1, 100, 20, September, Day(2026, 9, 20), 12f, 72.5m, 6.0417m, 1.5f),
            new CheckStudentRow(2, 101, 20, Day(2025, 9, 1), null, 8f, 48m, 6m, 1f)
        ], snapshot.Students.OrderBy(s => s.GbsId));
        Assert.Equal([
            new CheckTeacherRow(1, 100, 5, 8, September, Day(2026, 9, 1)),
            new CheckTeacherRow(2, 101, 6, 7, September, null)
        ], snapshot.Teachers.OrderBy(t => t.GbtId));
        Assert.Equal([new CheckDayTimeRow(1, 100, September, Day(2026, 12, 1), 1.5f)], snapshot.DayTimes);
        Assert.Equal(new Dictionary<int, SchedulePerson> { [20] = new("Gamma", "Cid", "6.020") }, snapshot.StudentNames);
        Assert.Equal(new Dictionary<int, CheckTeacherContract>
        {
            [5] = new(new SchedulePerson("Zeta", "Ann", "T5"), 7), [6] = new(new SchedulePerson("Zeta", "Ann", "T6"), null)
        }, snapshot.TeacherContracts);
        Assert.Equal(new Dictionary<int, string> { [7] = "S7", [8] = "S8" }, snapshot.SalarySchemeNames);
        Assert.Equal(Day(2027, 9, 1), snapshot.MaxFinishDate);
    }

    // part 20: only the rows of the year's groups, and the open end is the finish of that year
    [Fact]
    public async Task GetGroupRows_OfAYear_LoadsOnlyItsGroupsAndItsFinish()
    {
        // Arrange: group 100 of year 1, group 101 of year 2
        Seed();
        SetGroupYears();

        // Act
        GroupRowsSnapshot snapshot = await _repository.GetGroupRows(1);

        // Assert
        Assert.Equal([100], snapshot.Groups.Select(g => g.GroupId));
        Assert.Equal([1], snapshot.Students.Select(s => s.GbsId));
        Assert.Equal([1], snapshot.Teachers.Select(t => t.GbtId));
        Assert.Equal([1], snapshot.DayTimes.Select(d => d.GdtpId));
        Assert.Equal([5], snapshot.TeacherContracts.Keys);
        Assert.Equal(Day(2026, 9, 1), snapshot.MaxFinishDate);
    }

    //sync EF calls stay out of the async tests
    private void SetGroupYears()
    {
        foreach (Group group in _context.Groups)
        {
            group.AcademicYearId = group.GrpId == 100 ? 1 : 2;
        }

        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    // no academic years: the open end is no date (Access's Max over no rows is Null)
    [Fact]
    public async Task GetGroupRows_NoData_IsEmpty()
    {
        // Act
        GroupRowsSnapshot snapshot = await _repository.GetGroupRows(null);

        // Assert
        Assert.Empty(snapshot.Groups);
        Assert.Empty(snapshot.Students);
        Assert.Empty(snapshot.Teachers);
        Assert.Empty(snapshot.DayTimes);
        Assert.Empty(snapshot.StudentNames);
        Assert.Empty(snapshot.TeacherContracts);
        Assert.Null(snapshot.MaxFinishDate);
    }
}
