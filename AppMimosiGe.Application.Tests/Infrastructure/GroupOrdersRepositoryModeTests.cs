using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.Infrastructure;

//რეპოზიტორიის მეთოდები ვიზარდებისთვის (C4) ნამდვილ რეპოზიტორიაზე. მხოლოდ სინთეზური მონაცემები
public sealed class GroupOrdersRepositoryModeTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly DbContextOptions<MimosiGeDbContext> _options;
    private readonly GroupOrdersRepository _repository;

    public GroupOrdersRepositoryModeTests()
    {
        _options = new DbContextOptionsBuilder<MimosiGeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new MimosiGeDbContext(_options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new GroupOrdersRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private void Save()
    {
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    //ჯგუფი 4 ("605", წელი 11) საწყისი ბრძანებით 1 (ხაზები 101–104, სტრიქონები დაკავშირებულია), ჯგუფი 5 ("505", წელი
    //10) ბრძანებების გარეშე, და დაპაუზებული შექმნის ბრძანება 2
    private void Seed()
    {
        Group linked = StoredGroup(4, "605");
        Group other = StoredGroup(5, "505");
        other.AcademicYearId = 10;
        GroupOrder initial = StoredInitialOrder(linked, 1, 101);
        _context.Groups.AddRange(linked, other);
        _context.GroupOrders.AddRange(initial, new GroupOrder
        {
            Id = 2,
            OrderType = EGroupOrderType.CreateGroup,
            AcademicYearId = 11,
            OrderDate = Day(12, 1),
            IsPaused = true,
            GroupCode = "607",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            CreatedByUserName = "manager"
        });
        _context.SaveChanges();
    }

    //ვიზარდი 1 ყველა წლის ყველა ჯგუფს სტრიქონებით ტვირთავს, რომ სტრიქონებს კავშირი ჩაუწეროს
    [Fact]
    public async Task GetAllGroupsForChange_LoadsEveryGroupWithItsRowsTracked()
    {
        List<Group> groups = await _repository.GetAllGroupsForChange();

        Assert.Equal([4, 5], groups.Select(g => g.GrpId).Order());
        Assert.All(groups, g =>
        {
            Assert.Equal((1, 2, 1), (g.GroupsByTeachers.Count, g.GroupsByStudents.Count, g.GroupDayTimePlaces.Count));
            Assert.Equal(EntityState.Unchanged, _context.Entry(g).State);
        });
    }

    [Fact]
    public async Task GetModeForChange_WithoutRow_IsNull()
    {
        Assert.Null(await _repository.GetModeForChange());
    }

    //რეჟიმის სტრიქონი ემატება და მერე იცვლება, ორივე SaveChanges-ით (carcass-ის SaveChangesAsync-ს domain events
    //dispatcher სჭირდება, ამიტომ სინქრონულად)
    [Fact]
    public async Task AddMode_ThenGetModeForChange_ChangesTheSingleRow()
    {
        _repository.AddMode(new GroupOrdersMode
        {
            Id = GroupOrdersMode.SingleRowId, IsOrdersMode = true, TransitionDate = Day(11, 24)
        });
        Save();

        GroupOrdersMode mode = (await _repository.GetModeForChange())!;
        mode.IsOrdersMode = false;
        Save();

        await using var context = new MimosiGeDbContext(_options, true);
        GroupOrdersMode stored = await context.GroupOrdersMode.SingleAsync();
        Assert.Equal((GroupOrdersMode.SingleRowId, false, (DateTime?)Day(11, 24)),
            (stored.Id, stored.IsOrdersMode, stored.TransitionDate));
    }

    //ვიზარდი 2: ყველა ბრძანება ხაზებით, დაპაუზებულიც, Id-ის რიგით
    [Fact]
    public async Task GetAllOrdersForChange_LoadsEveryOrderWithItsLinesTracked()
    {
        List<GroupOrder> orders = await _repository.GetAllOrdersForChange();

        Assert.Equal([1, 2], orders.Select(o => o.Id));
        Assert.Equal((1, 2, 1),
            (orders[0].GroupOrderTeachers.Count, orders[0].GroupOrderStudents.Count,
                orders[0].GroupOrderDayTimePlaces.Count));
        Assert.True(orders[1].IsPaused);
        Assert.All(orders, o => Assert.Equal(EntityState.Unchanged, _context.Entry(o).State));
    }

    //ვიზარდი 2: მხოლოდ ხაზთან დაკავშირებული სტრიქონები; ჯგუფ 5-ის სტრიქონებს კავშირი არ აქვს
    [Fact]
    public async Task GetLinkedRowsForChange_LoadsOnlyLinkedRowsTracked()
    {
        GroupOrderLinkedRows rows = await _repository.GetLinkedRowsForChange();

        Assert.Equal([(41, (int?)101)], rows.Teachers.Select(r => (r.Id, r.GroupOrderTeacherId)));
        Assert.Equal([(42, (int?)102), (43, 103)],
            rows.Students.OrderBy(r => r.GbsId).Select(r => (r.GbsId, r.GroupOrderStudentId)));
        Assert.Equal([(44, (int?)104)], rows.DayTimePlaces.Select(r => (r.GdtpId, r.GroupOrderDayTimePlaceId)));
        Assert.Equal(EntityState.Unchanged, _context.Entry(rows.Students[0]).State);
    }
}
