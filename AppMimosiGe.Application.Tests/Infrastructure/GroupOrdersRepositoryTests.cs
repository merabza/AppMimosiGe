using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

//რეპოზიტორია და მისი ჩაწერის გაფართოებები (WriteStates, DeleteGroup) ნამდვილ რეპოზიტორიაზე. ჩაწერის შედეგი
//SaveChanges-ის შემდეგ ახალი კონტექსტით მოწმდება, როგორც ბაზაში
public sealed class GroupOrdersRepositoryTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly DbContextOptions<MimosiGeDbContext> _options;
    private readonly GroupOrdersRepository _repository;

    public GroupOrdersRepositoryTests()
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

    private static DateTime Day(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //ჯგუფები 1 ("601") და 2 ("602") საწყისი ბრძანებებით 1 და 2 (01.09) და გადაყვანა 3 (01.10): მოსწავლე 11 ჯგუფი 1-იდან
    //ჯგუფ 2-ში. სტრიქონები ამ ბრძანებების შედეგია და თავის ხაზებს უკავშირდება. ჯგუფ 3-ს ბრძანება არ აქვს. ყოველ ჯგუფს
    //ერთი გაკვეთილი აქვს მოსწავლეებით. მხოლოდ სინთეზური მონაცემები
    private void Seed()
    {
        _context.StudentContracts.AddRange(StudentContract(10), StudentContract(11), StudentContract(12),
            StudentContract(13));
        _context.Groups.AddRange(Group(1, "601"), Group(2, "602"), Group(3, "603"));

        GroupOrder initial1 = InitialOrder(1, 1, "601");
        initial1.GroupOrderTeachers.Add(TeacherLine(101, 1, Day(9, 1)));
        initial1.GroupOrderStudents.Add(StudentLine(201, 10, Day(9, 1)));
        initial1.GroupOrderStudents.Add(StudentLine(202, 11, Day(9, 1)));
        initial1.GroupOrderDayTimePlaces.Add(DayTimePlaceLine(301, 1, 1, Day(9, 1)));
        GroupOrder initial2 = InitialOrder(2, 2, "602");
        initial2.GroupOrderTeachers.Add(TeacherLine(111, 2, Day(9, 1)));
        initial2.GroupOrderStudents.Add(StudentLine(211, 12, Day(9, 1)));
        initial2.GroupOrderDayTimePlaces.Add(DayTimePlaceLine(311, 3, 2, Day(9, 1)));
        GroupOrder transfer = Order(3, EGroupOrderType.TransferStudents, Day(10, 1), 1);
        transfer.TargetGroupId = 2;
        transfer.NumberYear = 26;
        transfer.Number = 1;
        transfer.IssueDate = Day(9, 30);
        GroupOrderStudent transferLine = StudentLine(221, 11, null);
        transferLine.Note = "moved";
        transfer.GroupOrderStudents.Add(transferLine);
        _context.GroupOrders.AddRange(initial1, initial2, transfer);

        _context.GroupsByTeachers.AddRange(TeacherRow(11, 1, 101, 1, Day(9, 1)), TeacherRow(12, 2, 111, 2, Day(9, 1)));
        _context.GroupsByStudents.AddRange(StudentRow(21, 1, 201, 10, Day(9, 1), null),
            StudentRow(22, 1, 202, 11, Day(9, 1), Day(10, 1)), StudentRow(23, 2, 211, 12, Day(9, 1), null),
            StudentRow(24, 2, 221, 11, Day(10, 1), null, "moved"), StudentRow(25, 3, null, 13, Day(9, 1), null));
        _context.GroupDayTimePlaces.AddRange(DayTimePlaceRow(31, 1, 301, 1, 1, Day(9, 1)),
            DayTimePlaceRow(32, 2, 311, 3, 2, Day(9, 1)));
        _context.Lessons.AddRange(Lesson(1, 1, Day(9, 7)), Lesson(2, 2, Day(10, 7)), Lesson(3, 3, Day(9, 8)));
        _context.LessonsByStudents.AddRange(LessonStudent(1, 1, 10, 21), LessonStudent(2, 1, 11, 22),
            LessonStudent(3, 2, 12, 23), LessonStudent(4, 2, 11, 24), LessonStudent(5, 3, 13, 25));
        _context.SaveChanges();
    }

    //ჯგუფი 4 ("604"), შექმნილი ბრძანება 4-ით (02.11) და დახურული ბრძანება 5-ით (01.12): სტრიქონები 14, 26 და 34,
    //გაკვეთილი 4 მოსწავლით 6, და ჯგუფ 1-ის გაკვეთილის მოსწავლე 7, რომელიც ჯგუფ 4-ის სტრიქონს ებმის
    private void SeedCreatedGroup()
    {
        Group group = Group(4, "604");
        group.VoidDate = Day(12, 1);
        GroupOrder createOrder = Order(4, EGroupOrderType.CreateGroup, Day(11, 2), 4);
        SetFields(createOrder, "604");
        createOrder.GroupOrderTeachers.Add(TeacherLine(141, 1, null));
        createOrder.GroupOrderStudents.Add(StudentLine(241, 13, null));
        createOrder.GroupOrderDayTimePlaces.Add(DayTimePlaceLine(341, 1, 1, null));
        AddAndSave(group, createOrder, Order(5, EGroupOrderType.CloseGroup, Day(12, 1), 4),
            TeacherRow(14, 4, 141, 1, Day(11, 2)), StudentRow(26, 4, 241, 13, Day(11, 2), null),
            DayTimePlaceRow(34, 4, 341, 1, 1, Day(11, 2)), Lesson(4, 4, Day(11, 2)), LessonStudent(6, 4, 13, 26),
            LessonStudent(7, 1, 13, 26));
    }

    private MimosiGeDbContext NewContext()
    {
        return new MimosiGeDbContext(_options, true);
    }

    //carcass-ის SaveChangesAsync-ს domain events dispatcher სჭირდება, რომელსაც ტესტის კონსტრუქტორი არ აყენებს, ამიტომ
    //ცვლილებები სინქრონულად ინახება
    private void Save()
    {
        _context.SaveChanges();
    }

    private static void SaveChangesOf(MimosiGeDbContext context)
    {
        context.SaveChanges();
    }

    private void AddAndSave(params object[] entities)
    {
        _context.AddRange(entities);
        _context.SaveChanges();
        _context.ChangeTracker.Clear();
    }

    //ჩასაწერი ჯგუფი, როგორც მას შენახვა ტვირთავს
    private Group LoadGroup(int groupId)
    {
        return _context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).Single(g => g.GrpId == groupId);
    }

    //ჯგუფი სტრიქონებით ბაზიდან, ახალი კონტექსტით
    private Group StoredGroup(int groupId)
    {
        using MimosiGeDbContext context = NewContext();
        return context.Groups.AsNoTracking().Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).Single(g => g.GrpId == groupId);
    }

    private GroupOrder StoredOrder(int orderId)
    {
        using MimosiGeDbContext context = NewContext();
        return context.GroupOrders.AsNoTracking().Single(o => o.Id == orderId);
    }

    //Id-ები ბაზიდან, ზრდადობით
    private List<int> StoredIds(Func<MimosiGeDbContext, IQueryable<int>> ids)
    {
        using MimosiGeDbContext context = NewContext();
        return [.. ids(context).Order()];
    }

    //მოსწავლეების სტრიქონები: Id, ჯგუფი, ხაზი და დასრულება
    private List<(int GbsId, int GroupId, int? LineId, DateTime? EndDate)> StoredStudentRows()
    {
        using MimosiGeDbContext context = NewContext();
        return
        [
            .. context.GroupsByStudents.OrderBy(x => x.GbsId).ToList()
                .Select(x => (x.GbsId, x.GroupId, x.GroupOrderStudentId, x.EndDate))
        ];
    }

    //გაკვეთილების მოსწავლეები: Id და ჯგუფის სტრიქონი
    private List<(int Id, int? GroupByStudentId)> StoredLessonStudents()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.LessonsByStudents.OrderBy(x => x.Id).ToList().Select(x => (x.Id, x.GroupByStudentId))];
    }

    private List<(int NumberYear, int LastNumber)> StoredCounters()
    {
        using MimosiGeDbContext context = NewContext();
        return
        [
            .. context.GroupOrderNumbers.OrderBy(x => x.NumberYear).ToList().Select(x => (x.NumberYear, x.LastNumber))
        ];
    }

    private List<(int GrpId, bool DirtyLessons)> StoredDirtyLessons()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.Groups.OrderBy(x => x.GrpId).ToList().Select(x => (x.GrpId, x.DirtyLessons))];
    }

    private List<(int ScId, bool DirtyNextPayDate)> StoredDirtyNextPayDates()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.StudentContracts.OrderBy(x => x.ScId).ToList().Select(x => (x.ScId, x.DirtyNextPayDate))];
    }

    private static StudentContract StudentContract(int id)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = $"6.0{id}",
            StudentHumanId = id,
            PayerHumanId = id,
            AcademicYearId = 11,
            DirtyNextPayDate = false
        };
    }

    private static Group Group(int id, string code)
    {
        return new Group
        {
            GrpId = id,
            GroupCode = code,
            AcademicYearId = 11,
            CourseId = 1,
            GroupSizeId = 2,
            StudentStatusId = 1,
            DirtyLessons = false
        };
    }

    private static GroupOrder Order(int id, EGroupOrderType orderType, DateTime orderDate, int? groupId)
    {
        return new GroupOrder
        {
            Id = id,
            OrderType = orderType,
            AcademicYearId = 11,
            OrderDate = orderDate,
            GroupId = groupId,
            CreatedByUserName = "manager",
            CreatedAt = orderDate
        };
    }

    private static void SetFields(GroupOrder order, string groupCode)
    {
        order.GroupCode = groupCode;
        order.CourseId = 1;
        order.GroupSizeId = 2;
        order.StudentStatusId = 1;
    }

    private static GroupOrder InitialOrder(int id, int groupId, string groupCode)
    {
        GroupOrder order = Order(id, EGroupOrderType.Initial, Day(9, 1), groupId);
        SetFields(order, groupCode);
        return order;
    }

    //startDate მხოლოდ საწყისი ბრძანების ხაზს აქვს
    private static GroupOrderTeacher TeacherLine(int id, int teacherContractId, DateTime? startDate)
    {
        return new GroupOrderTeacher
        {
            Id = id,
            TeacherContractId = teacherContractId,
            SalarySchemaId = teacherContractId,
            StartDate = startDate
        };
    }

    //ტარიფი: კოეფიციენტი 1, 4 კვირაში 8 საათი (2-საათიანი განრიგით), საათის ფასი 6
    private static GroupOrderStudent StudentLine(int id, int studentContractId, DateTime? startDate)
    {
        return new GroupOrderStudent
        {
            Id = id,
            StudentContractId = studentContractId,
            FourWeekHours = 8,
            FourWeekFee = 48,
            OneHourFee = 6,
            HoursCoefficient = 1,
            StartDate = startDate
        };
    }

    private static GroupOrderDayTimePlace DayTimePlaceLine(int id, int weekDayId, int roomId, DateTime? startDate)
    {
        return new GroupOrderDayTimePlace
        {
            Id = id,
            WeekDayId = weekDayId,
            LessonStartTimeId = 1,
            HoursCount = 2,
            RoomId = roomId,
            StartDate = startDate
        };
    }

    private static GroupByTeacher TeacherRow(int id, int groupId, int lineId, int teacherContractId,
        DateTime startDate)
    {
        return new GroupByTeacher
        {
            Id = id,
            GroupId = groupId,
            TeacherContractId = teacherContractId,
            SalarySchemaId = teacherContractId,
            StartDate = startDate,
            GroupOrderTeacherId = lineId
        };
    }

    //ტარიფი ნაგულისხმევია: 8 საათი, 48, 6, კოეფიციენტი 1
    private static GroupByStudent StudentRow(int id, int groupId, int? lineId, int studentContractId,
        DateTime startDate, DateTime? endDate, string? note = null)
    {
        return new GroupByStudent
        {
            GbsId = id,
            GroupId = groupId,
            StudentContractId = studentContractId,
            StartDate = startDate,
            EndDate = endDate,
            Note = note,
            GroupOrderStudentId = lineId
        };
    }

    private static GroupDayTimePlace DayTimePlaceRow(int id, int groupId, int lineId, int weekDayId, int roomId,
        DateTime startDate)
    {
        return new GroupDayTimePlace
        {
            GdtpId = id,
            GroupId = groupId,
            WeekDayId = weekDayId,
            LessonStartTimeId = 1,
            HoursCount = 2,
            RoomId = roomId,
            StartDate = startDate,
            GroupOrderDayTimePlaceId = lineId
        };
    }

    private static Lesson Lesson(int id, int groupId, DateTime day)
    {
        return new Lesson
        {
            Id = id,
            GroupId = groupId,
            TeacherContractId = 1,
            SalarySchemaId = 1,
            LessonDt = day.AddHours(10)
        };
    }

    private static LessonByStudent LessonStudent(int id, int lessonId, int studentContractId, int groupByStudentId)
    {
        return new LessonByStudent
        {
            Id = id,
            LessonId = lessonId,
            StudentContractId = studentContractId,
            GroupByStudentId = groupByStudentId
        };
    }

    private static GroupOrderNewLines NewStudentLines(int lineId, GroupOrderStudent line)
    {
        return new GroupOrderNewLines(new Dictionary<int, GroupOrderTeacher>(),
            new Dictionary<int, GroupOrderStudent> { [lineId] = line }, new Dictionary<int, GroupOrderDayTimePlace>());
    }

    //ჯგუფის ბრძანებები ხაზებით: საწყისი ბრძანება და გადაყვანა, რომლის „საიდან" ჯგუფიც ის არის, თარიღის რიგით. ჯგუფის
    //ველები მხოლოდ საწყის და შექმნის ბრძანებას აქვს
    [Fact]
    public async Task GetOrders_ReturnsTheGroupOrdersWithTheirLinesInDateOrder()
    {
        // Act
        List<GroupOrderData> result = await _repository.GetOrders([1]);

        // Assert
        Assert.Equal([1, 3], result.Select(o => o.Id));
        GroupOrderData initial = result[0];
        Assert.Equal((EGroupOrderType.Initial, Day(9, 1), false, (int?)1, (int?)null, (DateTime?)null),
            (initial.OrderType, initial.OrderDate, initial.IsPaused, initial.GroupId, initial.TargetGroupId,
                initial.VoidDate));
        Assert.Equal(new GroupFields(11, "601", 1, 2, 1), initial.Fields);
        Assert.Equal([new GroupOrderTeacherLine(101, 1, 1, Day(9, 1))], initial.Teachers);
        Assert.Equal([
            new GroupOrderStudentLine(201, 10, 8f, 48m, 6m, 1f, null, Day(9, 1)),
            new GroupOrderStudentLine(202, 11, 8f, 48m, 6m, 1f, null, Day(9, 1))
        ], initial.Students);
        Assert.Equal([new GroupOrderDayTimePlaceLine(301, 1, 1, 2f, 1, Day(9, 1))], initial.DayTimePlaces);
        GroupOrderData transfer = result[1];
        Assert.Equal((EGroupOrderType.TransferStudents, Day(10, 1), (int?)1, (int?)2),
            (transfer.OrderType, transfer.OrderDate, transfer.GroupId, transfer.TargetGroupId));
        Assert.Null(transfer.Fields);
        Assert.Empty(transfer.Teachers);
        Assert.Equal([new GroupOrderStudentLine(221, 11, 8f, 48m, 6m, 1f, "moved")], transfer.Students);
        Assert.Empty(transfer.DayTimePlaces);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    //გადაყვანა სამიზნე ჯგუფის ბრძანებაცაა; სხვა ჯგუფის ბრძანება არ მოდის
    [Fact]
    public async Task GetOrders_TransferTarget_GetsTheTransferToo()
    {
        Assert.Equal([2, 3], (await _repository.GetOrders([2])).Select(o => o.Id));
        Assert.Equal([1, 2, 3], (await _repository.GetOrders([1, 2])).Select(o => o.Id));
        Assert.Empty(await _repository.GetOrders([3]));
        Assert.Empty(await _repository.GetOrders([]));
    }

    //ერთი დღის ბრძანებები Id-ის რიგით. დაპაუზებულ შექმნის ბრძანებას ჯგუფი არ აქვს, ამიტომ არ მოდის
    [Fact]
    public async Task GetOrders_SameDayOrdersInIdOrderWithoutPausedCreateOrders()
    {
        // Arrange
        GroupOrder paused = Order(8, EGroupOrderType.CreateGroup, Day(12, 1), null);
        paused.IsPaused = true;
        SetFields(paused, "605");
        AddAndSave(Order(7, EGroupOrderType.CloseGroup, Day(12, 1), 2),
            Order(6, EGroupOrderType.ChangeSchedule, Day(12, 1), 2), paused);

        // Act
        List<GroupOrderData> result = await _repository.GetOrders([1, 2]);

        // Assert
        Assert.Equal([1, 2, 3, 6, 7], result.Select(o => o.Id));
        Assert.All(result.Skip(3), o => Assert.Null(o.Fields));
    }

    //შექმნის ბრძანებას ჯგუფის ველები აქვს, გაუქმების თარიღი კი არა
    [Fact]
    public async Task GetOrders_CreateOrder_HasTheGroupFields()
    {
        // Arrange
        SeedCreatedGroup();

        // Act
        List<GroupOrderData> result = await _repository.GetOrders([4]);

        // Assert
        Assert.Equal([4, 5], result.Select(o => o.Id));
        Assert.Equal(new GroupFields(11, "604", 1, 2, 1), result[0].Fields);
        Assert.Null(result[0].VoidDate);
        Assert.Equal([new GroupOrderStudentLine(241, 13, 8f, 48m, 6m, 1f)], result[0].Students);
        Assert.Null(result[1].Fields);
    }

    [Fact]
    public async Task GetGroupsForChange_ReturnsTrackedGroupsWithTheirRows()
    {
        // Act
        List<Group> result = [.. (await _repository.GetGroupsForChange([1, 2, 99])).OrderBy(g => g.GrpId)];

        // Assert
        Assert.Equal([1, 2], result.Select(g => g.GrpId));
        Assert.All(result, g => Assert.Equal(EntityState.Unchanged, _context.Entry(g).State));
        Assert.Equal([11], result[0].GroupsByTeachers.Select(t => t.Id));
        Assert.Equal([21, 22], result[0].GroupsByStudents.Select(s => s.GbsId).Order());
        Assert.Equal([31], result[0].GroupDayTimePlaces.Select(d => d.GdtpId));
        Assert.Equal([12], result[1].GroupsByTeachers.Select(t => t.Id));
        Assert.Equal([23, 24], result[1].GroupsByStudents.Select(s => s.GbsId).Order());
        Assert.Equal([32], result[1].GroupDayTimePlaces.Select(d => d.GdtpId));
    }

    //რეჟიმის სტრიქონის არქონა ჯგუფების რეჟიმია (Recreate ცხრილს ცარიელს ტოვებს)
    [Fact]
    public async Task GetMode_NoRow_IsTheGroupsMode()
    {
        Assert.Equal(new GroupOrdersModeResponse(false, null, null, null), await _repository.GetMode());
    }

    [Fact]
    public async Task GetMode_ReturnsTheStoredModeWithoutTracking()
    {
        // Arrange
        AddAndSave(new GroupOrdersMode
        {
            Id = GroupOrdersMode.SingleRowId,
            IsOrdersMode = true,
            TransitionDate = Day(11, 24),
            SwitchedByUserName = "admin",
            SwitchedAt = Day(11, 24).AddHours(9)
        });

        // Act
        GroupOrdersModeResponse result = await _repository.GetMode();

        // Assert
        Assert.Equal(new GroupOrdersModeResponse(true, Day(11, 24), "admin", Day(11, 24).AddHours(9)), result);
        Assert.Empty(_context.ChangeTracker.Entries());
    }

    //წლის პირველი ნომერი 1-ია; მთვლელი იმავე SaveChanges-ით ემატება
    [Fact]
    public async Task NextNumber_FirstOfTheYear_IsOneAndAddsTheCounter()
    {
        // Act
        OrderNumber result = await _repository.NextNumber(26);
        Save();

        // Assert
        Assert.Equal(new OrderNumber(26, 1), result);
        Assert.Equal([(26, 1)], StoredCounters());
    }

    [Fact]
    public async Task NextNumber_ContinuesTheCounterOfItsYearOnly()
    {
        // Arrange
        AddAndSave(new GroupOrderNumber { NumberYear = 25, LastNumber = 40 },
            new GroupOrderNumber { NumberYear = 26, LastNumber = 14 });

        // Act
        OrderNumber result = await _repository.NextNumber(26);
        Save();

        // Assert
        Assert.Equal(new OrderNumber(26, 15), result);
        Assert.Equal([(25, 40), (26, 15)], StoredCounters());
    }

    //ერთ კონტექსტში (ერთ ტრანზაქციაში რამდენიმე ბრძანება) ნომრები ერთმანეთს მოსდევს, ახალ მთვლელზეც
    [Fact]
    public async Task NextNumber_TwiceInOneContext_GivesConsecutiveNumbers()
    {
        // Arrange
        AddAndSave(new GroupOrderNumber { NumberYear = 26, LastNumber = 5 });

        // Act
        List<OrderNumber> result =
        [
            await _repository.NextNumber(26), await _repository.NextNumber(26), await _repository.NextNumber(27),
            await _repository.NextNumber(27)
        ];
        Save();

        // Assert
        Assert.Equal([new OrderNumber(26, 6), new OrderNumber(26, 7), new OrderNumber(27, 1), new OrderNumber(27, 2)],
            result);
        Assert.Equal([(26, 7), (27, 2)], StoredCounters());
    }

    //ორი ერთდროული შენახვა ერთსა და იმავე ნომერს ითვლის: გვიანდელი SaveChanges მთლიანად უარყოფილია, ასე რომ ნომერი
    //მეორედ არ გაიცემა (LastNumber concurrency token-ია)
    [Fact]
    public async Task NextNumber_SavedConcurrently_RejectsTheLaterSave()
    {
        // Arrange
        AddAndSave(new GroupOrderNumber { NumberYear = 26, LastNumber = 5 });
        using MimosiGeDbContext otherContext = NewContext();
        OrderNumber first = await _repository.NextNumber(26);
        OrderNumber second = await new GroupOrdersRepository(otherContext).NextNumber(26);
        Save();

        // Act + Assert
        Assert.Throws<DbUpdateConcurrencyException>(() => SaveChangesOf(otherContext));
        Assert.Equal(new OrderNumber(26, 6), first);
        Assert.Equal(first, second);
        Assert.Equal([(26, 6)], StoredCounters());
    }

    [Fact]
    public void AddGroup_AddsTheGroup()
    {
        // Arrange
        var group = new Group { GroupCode = "605", AcademicYearId = 11 };

        // Act
        _repository.AddGroup(group);

        // Assert
        Assert.Equal(EntityState.Added, _context.Entry(group).State);
    }

    //მოსწავლის სტრიქონი იშლება თავისი ჩანაწერით გაკვეთილში; სხვა სტრიქონების ჩანაწერები და გაკვეთილები რჩება
    [Fact]
    public async Task RemoveRows_DeletesTheRowsWithTheLessonStudentsOfTheStudentRows()
    {
        // Arrange
        Group group = LoadGroup(2);

        // Act
        await _repository.RemoveRows(new GroupRemovedRows([group.GroupsByTeachers.Single()],
            [group.GroupsByStudents.Single(s => s.GbsId == 24)], [group.GroupDayTimePlaces.Single()]));
        Save();

        // Assert
        Assert.Equal([11], StoredIds(c => c.GroupsByTeachers.Select(t => t.Id)));
        Assert.Equal([21, 22, 23, 25], StoredIds(c => c.GroupsByStudents.Select(s => s.GbsId)));
        Assert.Equal([31], StoredIds(c => c.GroupDayTimePlaces.Select(d => d.GdtpId)));
        Assert.Equal([1, 2, 3, 5], StoredIds(c => c.LessonsByStudents.Select(x => x.Id)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.Lessons.Select(x => x.Id)));
    }

    [Fact]
    public async Task RemoveRows_WithoutStudentRows_DeletesNoLessonStudents()
    {
        // Arrange
        Group group = LoadGroup(1);

        // Act
        await _repository.RemoveRows(new GroupRemovedRows([group.GroupsByTeachers.Single()], [], []));
        Save();

        // Assert
        Assert.Equal([12], StoredIds(c => c.GroupsByTeachers.Select(t => t.Id)));
        Assert.Equal([1, 2, 3, 4, 5], StoredIds(c => c.LessonsByStudents.Select(x => x.Id)));
    }

    [Fact]
    public async Task HasSalary_FindsTheSalaryLineDetailsOfTheGroup()
    {
        // Arrange
        AddAndSave(new SalaryLineDetail { SadId = 1, SaId = 1, GroupId = 2 });

        // Act + Assert
        Assert.True(await _repository.HasSalary(2));
        Assert.False(await _repository.HasSalary(1));
    }

    //ჯგუფი იშლება გაკვეთილით, დასწრებით (სხვა ჯგუფის გაკვეთილის მოსწავლე 7-ის ჩათვლით, რომელიც მის სტრიქონს ებმის),
    //სტრიქონებით და ბრძანებებით ხაზებიანად. სხვა ჯგუფების მონაცემი რჩება
    [Fact]
    public async Task RemoveGroup_DeletesItsLessonsAttendanceRowsOrdersAndTheGroup()
    {
        // Arrange
        SeedCreatedGroup();
        Group group = LoadGroup(4);

        // Act
        await _repository.RemoveGroup(group);
        Save();

        // Assert
        Assert.Equal([1, 2, 3], StoredIds(c => c.Groups.Select(g => g.GrpId)));
        Assert.Equal([11, 12], StoredIds(c => c.GroupsByTeachers.Select(t => t.Id)));
        Assert.Equal([21, 22, 23, 24, 25], StoredIds(c => c.GroupsByStudents.Select(s => s.GbsId)));
        Assert.Equal([31, 32], StoredIds(c => c.GroupDayTimePlaces.Select(d => d.GdtpId)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.Lessons.Select(l => l.Id)));
        Assert.Equal([1, 2, 3, 4, 5], StoredIds(c => c.LessonsByStudents.Select(x => x.Id)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.GroupOrders.Select(o => o.Id)));
        Assert.Equal([101, 111], StoredIds(c => c.GroupOrderTeachers.Select(x => x.Id)));
        Assert.Equal([201, 202, 211, 221], StoredIds(c => c.GroupOrderStudents.Select(x => x.Id)));
        Assert.Equal([301, 311], StoredIds(c => c.GroupOrderDayTimePlaces.Select(x => x.Id)));
    }

    [Fact]
    public async Task GetStudentContractsForChange_ReturnsTrackedContracts()
    {
        // Act
        List<StudentContract> result = await _repository.GetStudentContractsForChange([10, 12, 99]);

        // Assert
        Assert.Equal([10, 12], result.Select(c => c.ScId).Order());
        Assert.All(result, c => Assert.Equal(EntityState.Unchanged, _context.Entry(c).State));
    }

    //გადაყვანის წაშლის გადათამაშება (D185): ჯგუფ 1-ის სტრიქონი 22 იმავე Id-ით ისევ ღიაა, ჯგუფ 2-ში გადაყვანილი
    //სტრიქონი 24 კი თავისი ჩანაწერით გაკვეთილში იშლება (D198). დანარჩენი სტრიქონები და გაკვეთილები უცვლელია
    [Fact]
    public async Task WriteStates_DeletedTransfer_ReopensTheRowAndRemovesTheTransferredOne()
    {
        // Arrange
        List<GroupOrderData> orders = await _repository.GetOrders([1, 2]);
        GroupOrdersReplayResult replay = GroupOrdersReplay.Run(orders.Single(o => o.Id == 3), null, orders);

        // Act
        await _repository.WriteStates(replay.States, GroupOrderNewLines.None, null, CancellationToken.None);
        Save();

        // Assert
        Assert.Null(replay.NotFitting);
        Assert.Equal([(21, 1, (int?)201, (DateTime?)null), (22, 1, 202, null), (23, 2, 211, null), (25, 3, null, null)],
            StoredStudentRows());
        Assert.Equal([(1, (int?)21), (2, 22), (3, 23), (5, 25)], StoredLessonStudents());
        Assert.Equal([11, 12], StoredIds(c => c.GroupsByTeachers.Select(t => t.Id)));
        Assert.Equal([31, 32], StoredIds(c => c.GroupDayTimePlaces.Select(d => d.GdtpId)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.Lessons.Select(l => l.Id)));
    }

    //შეხებულ ჯგუფს DirtyLessons ერთვება, მის ძველ და ახალ მოსწავლეებს კი შემდეგი გადახდის თარიღის გადათვლა: ჯგუფ
    //2-ს ტოვებს 12 (მოხსნილი სტრიქონი) და ემატება 13 (ახალი ხაზი). სხვა ჯგუფი და კონტრაქტი არ ინიშნება
    [Fact]
    public async Task WriteStates_MarksTheGroupsAndTheirOldAndNewStudentsDirty()
    {
        // Arrange
        GroupOrderStudent line = StudentLine(0, 13, null);
        GroupOrder order = Order(0, EGroupOrderType.AddStudents, Day(11, 2), 2);
        order.GroupOrderStudents.Add(line);
        _context.GroupOrders.Add(order);
        var state = new GroupState(new GroupFields(11, "602", 1, 2, 1), null,
            [new GroupTeacherRow(111, 2, 2, Day(9, 1), null)],
            [
                new GroupStudentRow(221, 11, 8f, 48m, 6m, 1f, "moved", Day(10, 1), null),
                new GroupStudentRow(-1, 13, 8f, 48m, 6m, 1f, null, Day(11, 2), null)
            ], [new GroupDayTimePlaceRow(311, 3, 1, 2f, 2, Day(9, 1), null)]);

        // Act
        await _repository.WriteStates(new Dictionary<int, GroupState> { [2] = state }, NewStudentLines(-1, line),
            null, CancellationToken.None);
        Save();

        // Assert
        Assert.Equal([(1, false), (2, true), (3, false)], StoredDirtyLessons());
        Assert.Equal([(10, false), (11, true), (12, true), (13, true)], StoredDirtyNextPayDates());
    }

    //ჯგუფი, რომლის ველები, თარიღები და ტარიფები ბრძანებებმა შეცვალა: სტრიქონები იმავე Id-ებით რჩება და ახალ
    //მნიშვნელობებს იღებს, მათზე მიბმული გაკვეთილების მოსწავლეები კი უცვლელია
    [Fact]
    public async Task WriteStates_ChangedRows_KeepTheirIdsAndGetTheNewValues()
    {
        // Arrange
        var state = new GroupState(new GroupFields(11, "611", 1, 2, 1), Day(12, 20),
            [new GroupTeacherRow(101, 1, 2, Day(9, 1), null)],
            [
                new GroupStudentRow(201, 10, 12f, 84m, 7m, 1.5f, "fee", Day(9, 1), Day(12, 20)),
                new GroupStudentRow(202, 11, 8f, 48m, 6m, 1f, null, Day(9, 1), Day(10, 1))
            ], [new GroupDayTimePlaceRow(301, 1, 1, 3f, 2, Day(9, 1), null)]);

        // Act
        IReadOnlyDictionary<int, Group> result = await _repository.WriteStates(
            new Dictionary<int, GroupState> { [1] = state }, GroupOrderNewLines.None, null, CancellationToken.None);
        Save();

        // Assert
        Assert.Equal([1], result.Keys);
        Group group = StoredGroup(1);
        Assert.Equal(("611", (DateTime?)Day(12, 20)), (group.GroupCode, group.VoidDate));
        GroupByTeacher teacher = Assert.Single(group.GroupsByTeachers);
        Assert.Equal((11, 2), (teacher.Id, teacher.SalarySchemaId));
        Assert.Equal([21, 22], group.GroupsByStudents.Select(s => s.GbsId).Order());
        GroupByStudent student = group.GroupsByStudents.Single(s => s.GbsId == 21);
        Assert.Equal((12f, 84m, 7m, 1.5f, "fee", (DateTime?)Day(12, 20)),
            (student.FourWeekHours, student.FourWeekFee, student.OneHourFee, student.HoursCoefficient, student.Note,
                student.EndDate));
        GroupDayTimePlace dayTimePlace = Assert.Single(group.GroupDayTimePlaces);
        Assert.Equal((31, 3f, 2), (dayTimePlace.GdtpId, dayTimePlace.HoursCount, dayTimePlace.RoomId));
        Assert.Equal([(1, (int?)21), (2, 22), (3, 23), (4, 24), (5, 25)], StoredLessonStudents());
    }

    //ახალი ბრძანება 3 (მოსწავლის დამატება), როგორც მას შენახვა ჩაწერს: გადათამაშება, ნომერი (26.1-ის შემდეგ) და
    //ჩაწერა ერთი SaveChanges-ით. ახალი სტრიქონი ბრძანების ახალ ხაზს უკავშირდება, ძველი სტრიქონები იმავე Id-ებით რჩება
    [Fact]
    public async Task WriteStates_NewOrder_AddsTheRowOfItsNewLine()
    {
        // Arrange
        AddAndSave(new GroupOrderNumber { NumberYear = 26, LastNumber = 1 });
        GroupOrderStudent line = StudentLine(0, 13, null);
        GroupOrder order = Order(0, EGroupOrderType.AddStudents, Day(11, 2), 1);
        order.GroupOrderStudents.Add(line);
        _context.GroupOrders.Add(order);
        var after = new GroupOrderData
        {
            Id = int.MaxValue,
            OrderType = EGroupOrderType.AddStudents,
            OrderDate = Day(11, 2),
            GroupId = 1,
            Students = [new GroupOrderStudentLine(-1, 13, null, 48m, 6m, 1f)]
        };
        GroupOrdersReplayResult replay = GroupOrdersReplay.Run(null, after, await _repository.GetOrders([1, 2]));
        OrderNumber number = await _repository.NextNumber(26);
        order.NumberYear = number.NumberYear;
        order.Number = number.Number;

        // Act
        await _repository.WriteStates(replay.States, NewStudentLines(-1, line), null, CancellationToken.None);
        Save();

        // Assert
        Assert.Null(replay.NotFitting);
        Assert.Equal([1], replay.States.Keys);
        GroupByStudent added = StoredGroup(1).GroupsByStudents.Single(s => s.GroupOrderStudentId == line.Id);
        Assert.Equal((13, 8f, Day(11, 2), (DateTime?)null),
            (added.StudentContractId, added.FourWeekHours, added.StartDate, added.EndDate));
        Assert.Equal([
            (21, 1, (int?)201, (DateTime?)null), (22, 1, 202, Day(10, 1)), (23, 2, 211, null), (24, 2, 221, null),
            (25, 3, null, null), (added.GbsId, 1, line.Id, null)
        ], StoredStudentRows());
        Assert.Equal((26, (int?)2), (number.NumberYear, StoredOrder(order.Id).Number));
        Assert.Equal([(26, 2)], StoredCounters());
    }

    //შექმნის ბრძანება ჯგუფს ქმნის (newGroupId): ჯგუფი და მისი სტრიქონები ემატება და ბრძანების ახალ ხაზებს
    //უკავშირდება, ბრძანება კი ჯგუფს დაბრუნებული ობიექტით
    [Fact]
    public async Task WriteStates_NewGroup_AddsTheGroupWithItsRows()
    {
        // Arrange
        GroupOrderTeacher teacherLine = TeacherLine(0, 2, null);
        GroupOrderStudent studentLine = StudentLine(0, 13, null);
        GroupOrderDayTimePlace dayTimePlaceLine = DayTimePlaceLine(0, 1, 1, null);
        GroupOrder order = Order(0, EGroupOrderType.CreateGroup, Day(11, 2), null);
        SetFields(order, "605");
        order.GroupOrderTeachers.Add(teacherLine);
        order.GroupOrderStudents.Add(studentLine);
        order.GroupOrderDayTimePlaces.Add(dayTimePlaceLine);
        _context.GroupOrders.Add(order);
        var data = new GroupOrderData
        {
            Id = int.MaxValue,
            OrderType = EGroupOrderType.CreateGroup,
            OrderDate = Day(11, 2),
            GroupId = -1,
            Fields = new GroupFields(11, "605", 1, 2, 1),
            Teachers = [new GroupOrderTeacherLine(-1, 2, 2)],
            Students = [new GroupOrderStudentLine(-2, 13, null, 48m, 6m, 1f)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(-3, 1, 1, 2f, 1)]
        };
        GroupOrdersReplayResult replay = GroupOrdersReplay.Run(null, data, []);
        var newLines = new GroupOrderNewLines(new Dictionary<int, GroupOrderTeacher> { [-1] = teacherLine },
            new Dictionary<int, GroupOrderStudent> { [-2] = studentLine },
            new Dictionary<int, GroupOrderDayTimePlace> { [-3] = dayTimePlaceLine });

        // Act
        IReadOnlyDictionary<int, Group> result = await _repository.WriteStates(replay.States, newLines, -1,
            CancellationToken.None);
        order.Group = result[-1];
        Save();

        // Assert
        Group group = StoredGroup(result[-1].GrpId);
        Assert.Equal(("605", 11, 1, true, (DateTime?)null),
            (group.GroupCode, group.AcademicYearId, group.CourseId, group.DirtyLessons, group.VoidDate));
        Assert.Equal(group.GrpId, StoredOrder(order.Id).GroupId);
        Assert.Equal(teacherLine.Id, Assert.Single(group.GroupsByTeachers).GroupOrderTeacherId);
        GroupByStudent student = Assert.Single(group.GroupsByStudents);
        Assert.Equal(((int?)studentLine.Id, 13, 8f, Day(11, 2)),
            (student.GroupOrderStudentId, student.StudentContractId, student.FourWeekHours, student.StartDate));
        Assert.Equal(dayTimePlaceLine.Id, Assert.Single(group.GroupDayTimePlaces).GroupOrderDayTimePlaceId);
        Assert.Equal([(10, false), (11, false), (12, false), (13, true)], StoredDirtyNextPayDates());
    }

    //D191: ჯგუფი 4 იშლება, მისი მოსწავლის კონტრაქტს კი შემდეგი გადახდის თარიღის გადათვლა ერთვება
    [Fact]
    public async Task DeleteGroup_DeletesTheGroupAndMarksItsStudentsDirty()
    {
        // Arrange
        SeedCreatedGroup();

        // Act
        Result result = await _repository.DeleteGroup(4, CancellationToken.None);
        Save();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], StoredIds(c => c.Groups.Select(g => g.GrpId)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.GroupOrders.Select(o => o.Id)));
        Assert.Equal([1, 2, 3], StoredIds(c => c.Lessons.Select(l => l.Id)));
        Assert.Equal([(10, false), (11, false), (12, false), (13, true)], StoredDirtyNextPayDates());
    }

    //D191: ჯგუფი, რომლის გაკვეთილიც ხელფასის დათვლაშია, არ იშლება და არაფერი იცვლება
    [Fact]
    public async Task DeleteGroup_GroupInSalary_IsRefusedAndChangesNothing()
    {
        // Arrange
        SeedCreatedGroup();
        AddAndSave(new SalaryLineDetail { SadId = 1, SaId = 1, GroupId = 4 });

        // Act
        Result result = await _repository.DeleteGroup(4, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasSalary), result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Empty(_context.ChangeTracker.Entries());
    }
}
