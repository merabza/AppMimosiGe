using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class AcademicYearsRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly AcademicYearsRepository _repository;

    public AcademicYearsRepositoryTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new AcademicYearsRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Date(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //years 11 (2026-2027) and 10 (2025-2026); groups of 2026-2027: 100 (open), 101 (void after the year's end), 102
    //(void on 01.06.2027), 103 (void on the year's end), 104 (void on 01.03.2027): more groups void before the end than
    //after it; group 200 of 2025-2026 (open); one contract of each year
    private void Seed()
    {
        //carcass SaveChangesAsync needs a domain events dispatcher the design-time constructor does not set,
        //so test data is saved synchronously
        _context.AcademicYears.AddRange(
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = Date(2026, 9, 1), FinishDate = Date(2027, 9, 1)
            },
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = Date(2025, 9, 1), FinishDate = Date(2026, 9, 1)
            });
        _context.Courses.AddRange(new Course { CrsId = 1, CourseName = "English" },
            new Course { CrsId = 2, CourseName = "Math" });
        _context.Groups.AddRange(Group(100, "E1", 11, 1, null), Group(101, "E2", 11, 2, Date(2027, 12, 1)),
            Group(102, "E3", 11, 1, Date(2027, 6, 1)), Group(103, "E4", 11, 1, Date(2027, 9, 1)),
            Group(104, "E5", 11, 1, Date(2027, 3, 1)), Group(200, "O1", 10, 1, null));
        _context.StudentContracts.AddRange(
            new StudentContract
            {
                ScId = 1, ContractNumber = "6.001", StudentHumanId = 1, PayerHumanId = 1, AcademicYearId = 11
            },
            new StudentContract
            {
                ScId = 2, ContractNumber = "5.001", StudentHumanId = 1, PayerHumanId = 1, AcademicYearId = 10
            });
        _context.SaveChanges();
    }

    private static Group Group(int id, string code, int academicYearId, int courseId, DateTime? voidDate) => new()
    {
        GrpId = id, GroupCode = code, AcademicYearId = academicYearId, CourseId = courseId, VoidDate = voidDate
    };

    [Fact]
    public async Task GetAcademicYears_ReturnsEveryYear()
    {
        List<AcademicYear> years = await _repository.GetAcademicYears();

        Assert.Equal([10, 11], years.Select(y => y.AyId).Order());
    }

    // an open group: no void date, or one after the year's end
    [Fact]
    public async Task GetAcademicYearsInfo_CountsContractsGroupsAndOpenGroupsByStart()
    {
        List<AcademicYearInfoResponse> info = await _repository.GetAcademicYearsInfo();

        Assert.Equal([
            new AcademicYearInfoResponse(10, "2025-2026", Date(2025, 9, 1), Date(2026, 9, 1), 1, 1, 1),
            new AcademicYearInfoResponse(11, "2026-2027", Date(2026, 9, 1), Date(2027, 9, 1), 1, 5, 2)
        ], info);
    }

    // the groups of the year still open on the close date: no void date or a later one, by id
    [Fact]
    public async Task GetGroupsToClose_AreTheYearsGroupsOpenOnTheCloseDate()
    {
        List<GroupToClose> groups = await _repository.GetGroupsToClose(11, Date(2027, 9, 1));

        Assert.Equal([new GroupToClose(100, "E1", "English"), new GroupToClose(101, "E2", "Math")], groups);
    }

    // a group added later with a smaller id comes first: the groups are closed in the order of their ids
    [Fact]
    public async Task GetGroupsToClose_AreInIdOrder()
    {
        // Arrange
        AddGroup(Group(99, "E0", 11, 1, null));

        // Act
        List<GroupToClose> groups = await _repository.GetGroupsToClose(11, Date(2027, 9, 1));

        // Assert
        Assert.Equal([99, 100, 101], groups.Select(g => g.GrpId));
    }

    //sync EF calls stay out of the async tests
    private void AddGroup(Group group)
    {
        _context.Groups.Add(group);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetGroupsToClose_EarlierCloseDate_TakesTheLaterVoidsToo()
    {
        List<GroupToClose> groups = await _repository.GetGroupsToClose(11, Date(2027, 5, 1));

        Assert.Equal([100, 101, 102, 103], groups.Select(g => g.GrpId));
    }

    //sync EF calls stay out of the async tests
    [Fact]
    public void Add_SavesTheYear()
    {
        _repository.Add(new AcademicYear
        {
            AcademicYearName = "2027-2028", StartDate = Date(2027, 9, 1), FinishDate = Date(2028, 9, 1)
        });
        _context.SaveChanges();

        Assert.Single(_context.AcademicYears.AsNoTracking(), y => y.AcademicYearName == "2027-2028");
    }
}
