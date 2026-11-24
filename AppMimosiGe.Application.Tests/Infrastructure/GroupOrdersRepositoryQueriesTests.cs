using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

//რეპოზიტორიის წაკითხვის მეთოდები (C3) ნამდვილ რეპოზიტორიაზე. მხოლოდ სინთეზური მონაცემები, ნამდვილი სახელების გარეშე
public sealed class GroupOrdersRepositoryQueriesTests : IDisposable
{
    private readonly MimosiGeDbContext _context;
    private readonly GroupOrdersRepository _repository;

    public GroupOrdersRepositoryQueriesTests()
    {
        DbContextOptions<MimosiGeDbContext> options = new DbContextOptionsBuilder<MimosiGeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _context = new MimosiGeDbContext(options, true);
        Seed();
        _context.ChangeTracker.Clear();
        _repository = new GroupOrdersRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    private static DateTime Day(int month, int day, int year = 2026) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    //წლის 11 ჯგუფები 1 ("601") და 2 ("602") საწყისი ბრძანებებით 1 და 2 (01.09), ჯგუფი 3 ("1001") ბრძანებების გარეშე,
    //და წლის 10 ჯგუფი 4 ("501"). ბრძანებები: 3 გადაყვანა 1-იდან 2-ში (01.10, 26.1), 4 მასწავლებლის შეცვლა ჯგუფ 1-ში
    //(01.12, 26.2), 5 დაპაუზებული შექმნის ბრძანება "1002" (01.12, 26.3), 6 ჯგუფ 4-ის დახურვა (30.06, 25.1),
    //7 დამატება (05.12, 26.10) და 8 გაყვანა (02.12, 26.9) ჯგუფ 2-ში
    private void Seed()
    {
        _context.AcademicYears.AddRange(
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = Day(9, 1, 2025), FinishDate = Day(9, 1)
            },
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = Day(9, 1), FinishDate = Day(9, 1, 2027)
            });
        _context.Courses.AddRange(new Course { CrsId = 6, CourseName = "Math" },
            new Course { CrsId = 7, CourseName = "English" });
        _context.GroupSizes.AddRange(new GroupSize { GrsId = 2, GrsSize = 4, GrsName = "Four" },
            new GroupSize { GrsId = 3, GrsSize = 2, GrsName = "Pair" });
        _context.StudentStatuses.AddRange(new StudentStatus { Id = 10, StudentStatusName = "Tenth", Rate = 110 },
            new StudentStatus { Id = 1, StudentStatusName = "First", Rate = 101 });
        _context.Humans.AddRange(Human(1, "Delta", "Dan"), Human(2, "Epsilon", "Eva"), Human(20, "Beta", "Bob"),
            Human(21, "Alpha", "Ann"));
        _context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T1.05", TeacherHumanId = 1, RsCountryId = 1 },
            new TeacherContract { Id = 6, ContractNumber = "T1.06", TeacherHumanId = 2, RsCountryId = 1 });
        _context.TeacherSalarySchemes.AddRange(new TeacherSalaryScheme { Id = 8, SchemaName = "Main" },
            new TeacherSalaryScheme { Id = 9, SchemaName = "Hourly" });
        _context.StudentContracts.AddRange(StudentContract(20, "6.020"), StudentContract(21, "6.021"));
        _context.WeekDays.AddRange(new WeekDay { Id = 1, Name = "Monday", ShortName = "1-Mo", WeekDayNumber = 1 },
            new WeekDay { Id = 3, Name = "Wednesday", ShortName = "3-We", WeekDayNumber = 3 });
        _context.LessonStartTimes.AddRange(new LessonStartTime { LstId = 4, LstTime = new TimeOnly(15, 0) },
            new LessonStartTime { LstId = 2, LstTime = new TimeOnly(9, 30) });
        _context.Rooms.AddRange(new Room { Id = 1, RoomName = "Room 1" }, new Room { Id = 2, RoomName = "Room 2" });
        _context.Groups.AddRange(Group(1, "601", 11, 3), Group(2, "602", 11, 3), Group(3, "1001", 11, 2),
            Group(4, "501", 10, 2));

        GroupOrder initial1 = Order(1, EGroupOrderType.Initial, Day(9, 1), 1);
        SetFields(initial1, "601", 3);
        initial1.GroupOrderTeachers.Add(new GroupOrderTeacher
        {
            Id = 101, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Day(9, 1)
        });
        initial1.GroupOrderStudents.Add(StudentLine(202, 21, Day(9, 1)));
        initial1.GroupOrderStudents.Add(StudentLine(201, 20, Day(9, 1)));
        initial1.GroupOrderDayTimePlaces.Add(new GroupOrderDayTimePlace
        {
            Id = 301, WeekDayId = 1, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1, StartDate = Day(9, 1)
        });
        GroupOrder initial2 = Order(2, EGroupOrderType.Initial, Day(9, 1), 2);
        SetFields(initial2, "602", 3);
        GroupOrder transfer = Numbered(Order(3, EGroupOrderType.TransferStudents, Day(10, 1), 1), 26, 1);
        transfer.TargetGroupId = 2;
        GroupOrder changeTeacher = Numbered(Order(4, EGroupOrderType.ChangeTeacher, Day(12, 1), 1), 26, 2);
        changeTeacher.GroupOrderTeachers.Add(new GroupOrderTeacher { Id = 401, TeacherContractId = 6, SalarySchemaId = 9 });
        GroupOrder paused = Numbered(Order(5, EGroupOrderType.CreateGroup, Day(12, 1), null), 26, 3);
        paused.IsPaused = true;
        SetFields(paused, "1002", 2);
        GroupOrder close = Numbered(Order(6, EGroupOrderType.CloseGroup, Day(6, 30), 4), 25, 1);
        close.AcademicYearId = 10;
        _context.GroupOrders.AddRange(initial1, initial2, transfer, changeTeacher, paused, close,
            Numbered(Order(7, EGroupOrderType.AddStudents, Day(12, 5), 2), 26, 10),
            Numbered(Order(8, EGroupOrderType.RemoveStudents, Day(12, 2), 2), 26, 9));

        _context.GroupsByTeachers.AddRange(
            new GroupByTeacher
            {
                Id = 11, GroupId = 1, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Day(9, 1),
                EndDate = Day(12, 1), GroupOrderTeacherId = 101
            },
            new GroupByTeacher
            {
                Id = 12, GroupId = 1, TeacherContractId = 6, SalarySchemaId = 9, StartDate = Day(12, 1),
                GroupOrderTeacherId = 401
            });
        _context.GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 21, GroupId = 1, StudentContractId = 20, FourWeekHours = 8, FourWeekFee = 48, OneHourFee = 6,
            HoursCoefficient = 1, Note = "note", StartDate = Day(9, 1), EndDate = Day(10, 1), GroupOrderStudentId = 201
        });
        _context.GroupDayTimePlaces.Add(new GroupDayTimePlace
        {
            GdtpId = 31, GroupId = 1, WeekDayId = 1, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1,
            StartDate = Day(9, 1), GroupOrderDayTimePlaceId = 301
        });
        _context.SaveChanges();
    }

    private static Human Human(int id, string lastName, string firstName)
    {
        return new Human { HumId = id, LastName = lastName, FirstName = firstName, PersonalId = $"0100000000{id}" };
    }

    private static StudentContract StudentContract(int id, string number)
    {
        return new StudentContract
        {
            ScId = id,
            ContractNumber = number,
            StudentHumanId = id,
            PayerHumanId = id,
            AcademicYearId = 11,
            DirtyNextPayDate = false
        };
    }

    private static Group Group(int id, string code, int academicYearId, int groupSizeId)
    {
        return new Group
        {
            GrpId = id,
            GroupCode = code,
            AcademicYearId = academicYearId,
            CourseId = 6,
            GroupSizeId = groupSizeId,
            StudentStatusId = 10,
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

    private static GroupOrder Numbered(GroupOrder order, int numberYear, int number)
    {
        order.NumberYear = numberYear;
        order.Number = number;
        order.IssueDate = order.OrderDate.AddDays(-1);
        order.OrganizationName = "Organization";
        order.SignerTitle = "Director";
        order.SignerName = "Signer";
        return order;
    }

    private static void SetFields(GroupOrder order, string groupCode, int groupSizeId)
    {
        order.GroupCode = groupCode;
        order.CourseId = 6;
        order.GroupSizeId = groupSizeId;
        order.StudentStatusId = 10;
    }

    //ტარიფი: კოეფიციენტი 1, 4 კვირაში 8 საათი, საათის ფასი 6
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

    private static GroupOrdersListQuery ListQuery(int offset = 0, int rowsCount = 20, int? academicYearId = null,
        EGroupOrderType? orderType = null, int? groupId = null, DateTime? dateFrom = null, DateTime? dateTo = null,
        bool? isPaused = null, IReadOnlyList<GroupOrderSortField>? sortFields = null)
    {
        return new GroupOrdersListQuery(offset, rowsCount, academicYearId, orderType, groupId, dateFrom, dateTo,
            isPaused, sortFields ?? GroupOrdersListQueryFactory.DefaultSortFields);
    }

    //სახელები Id-ის რიგით
    private static void AssertNames(IEnumerable<(int Id, string Name)> expected, IReadOnlyDictionary<int, string> actual)
    {
        Assert.Equal(expected, actual.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)));
    }

    private async Task AssertRows(GroupOrdersListQuery query, params int[] expectedIds)
    {
        int[] ids = await RowIds(query);
        Assert.Equal(expectedIds, ids);
    }

    private async Task<int[]> RowIds(GroupOrdersListQuery query)
    {
        GroupOrdersRowsDataResponse rows = await _repository.GetRowsData(query);
        return [.. rows.Rows.Select(r => r.Id)];
    }

    //D217: ნომრით, ახალი პირველი (26.10 > 26.9 > … > 25.1), ნომრის გარეშე საწყისი ბრძანებები ბოლოს
    [Fact]
    public async Task GetRowsData_DefaultSort_ListsTheNewestNumberFirst()
    {
        // Act
        GroupOrdersRowsDataResponse rows = await _repository.GetRowsData(ListQuery());

        // Assert
        Assert.Equal(8, rows.AllRowsCount);
        Assert.Equal(0, rows.Offset);
        Assert.Equal([7, 8, 5, 4, 3, 6, 1, 2], rows.Rows.Select(r => r.Id));
        Assert.Equal(["26.10", "26.9", "26.3", "26.2", "26.1", "25.1", null, null], rows.Rows.Select(r => r.Number));
    }

    //დაპაუზებულ შექმნის ბრძანებას ჯგუფი ჯერ არ აქვს: კოდი მისია; გადაყვანას სამიზნე ჯგუფიც აქვს
    [Fact]
    public async Task GetRowsData_MapsTheRow()
    {
        // Act
        GroupOrdersRowsDataResponse rows = await _repository.GetRowsData(ListQuery());

        // Assert
        Assert.Equal(new GroupOrderRowResponse(3, (int)EGroupOrderType.TransferStudents, "26.1", Day(9, 30),
            Day(10, 1), false, 11, "2026-2027", 1, "601", 2, "602"), rows.Rows.Single(r => r.Id == 3));
        Assert.Equal(new GroupOrderRowResponse(5, (int)EGroupOrderType.CreateGroup, "26.3", Day(11, 30),
            Day(12, 1), true, 11, "2026-2027", null, "1002", null, null), rows.Rows.Single(r => r.Id == 5));
        Assert.Equal(new GroupOrderRowResponse(1, (int)EGroupOrderType.Initial, null, null, Day(9, 1), false, 11,
            "2026-2027", 1, "601", null, null), rows.Rows.Single(r => r.Id == 1));
    }

    [Fact]
    public async Task GetRowsData_FiltersByTheYearAndType()
    {
        await AssertRows(ListQuery(academicYearId: 10), 6);
        await AssertRows(ListQuery(academicYearId: 11), 7, 8, 5, 4, 3, 1, 2);
        await AssertRows(ListQuery(orderType: EGroupOrderType.TransferStudents), 3);
    }

    //გადაყვანა ორივე ჯგუფის ბრძანებაა
    [Fact]
    public async Task GetRowsData_GroupFilter_FindsTheTransfersToTheGroupToo()
    {
        await AssertRows(ListQuery(groupId: 2), 7, 8, 3, 2);
        await AssertRows(ListQuery(groupId: 1), 4, 3, 1);
    }

    //D216: ბრძანების თარიღი, ორივე ჩათვლით
    [Fact]
    public async Task GetRowsData_DatesFilter_TheOrderDateBothInclusive()
    {
        await AssertRows(ListQuery(dateFrom: Day(12, 1), dateTo: Day(12, 2)), 8, 5, 4);
        await AssertRows(ListQuery(dateFrom: Day(12, 3)), 7);
        await AssertRows(ListQuery(dateTo: Day(9, 1)), 6, 1, 2);
    }

    [Fact]
    public async Task GetRowsData_PausedFilter()
    {
        await AssertRows(ListQuery(isPaused: true), 5);
        await AssertRows(ListQuery(isPaused: false), 7, 8, 4, 3, 6, 1, 2);
    }

    //ნომრის ზრდადობით ნომრის გარეშე ბრძანებები თავშია, Id-ის რიგით
    [Fact]
    public async Task GetRowsData_NumberAscending_ComparesTheNumbersAsNumbers()
    {
        await AssertRows(ListQuery(sortFields: [new GroupOrderSortField(EGroupOrderSortField.Number, true)]), 1, 2,
            6, 3, 4, 5, 8, 7);
    }

    [Theory]
    [InlineData(EGroupOrderSortField.GroupCode, true, new[] { 5, 6, 1, 3, 4, 2, 7, 8 })]
    [InlineData(EGroupOrderSortField.TargetGroupCode, false, new[] { 3, 1, 2, 4, 5, 6, 7, 8 })]
    [InlineData(EGroupOrderSortField.OrderType, true, new[] { 1, 2, 5, 4, 7, 8, 3, 6 })]
    [InlineData(EGroupOrderSortField.IssueDate, false, new[] { 7, 8, 4, 5, 3, 6, 1, 2 })]
    [InlineData(EGroupOrderSortField.IsPaused, false, new[] { 5, 1, 2, 3, 4, 6, 7, 8 })]
    [InlineData(EGroupOrderSortField.AcademicYearName, true, new[] { 6, 1, 2, 3, 4, 5, 7, 8 })]
    public async Task GetRowsData_SortsByTheField(EGroupOrderSortField field, bool ascending, int[] expectedIds)
    {
        await AssertRows(ListQuery(sortFields: [new GroupOrderSortField(field, ascending)]), expectedIds);
    }

    //ფილტრის შეცვლის შემდეგ გვერდი შეიძლება აღარ არსებობდეს: მაშინ ბოლო გვერდი ჩანს
    [Fact]
    public async Task GetRowsData_OffsetPastTheEnd_ShowsTheLastPage()
    {
        // Act
        GroupOrdersRowsDataResponse rows = await _repository.GetRowsData(ListQuery(30, 3));

        // Assert
        Assert.Equal(6, rows.Offset);
        Assert.Equal([1, 2], rows.Rows.Select(r => r.Id));
    }

    //მოსწავლეები „გვარი სახელი / ნომერი"-თ ლაგდება, ხაზები თავის Id-ს ინარჩუნებს
    [Fact]
    public async Task GetOne_ReturnsTheOrderWithItsLines()
    {
        // Act
        GroupOrderResponse? order = await _repository.GetOne(1);

        // Assert
        Assert.NotNull(order);
        Assert.Equal((int)EGroupOrderType.Initial, order.OrderType);
        Assert.Null(order.Number);
        Assert.Equal(1, order.GroupId);
        Assert.Equal("601", order.GroupCode);
        Assert.Equal(6, order.CourseId);
        Assert.Equal(3, order.GroupSizeId);
        Assert.Equal(10, order.StudentStatusId);
        Assert.Equal("manager", order.CreatedByUserName);
        Assert.Equal([new GroupOrderTeacherResponse(101, 5, 8, Day(9, 1), null)], order.Teachers);
        Assert.Equal([
            new GroupOrderStudentResponse(202, 21, "Alpha Ann / 6.021", 8f, 48m, 6m, 1f, null, Day(9, 1), null),
            new GroupOrderStudentResponse(201, 20, "Beta Bob / 6.020", 8f, 48m, 6m, 1f, null, Day(9, 1), null)
        ], order.Students);
        Assert.Equal([new GroupOrderDayTimePlaceResponse(301, 1, 4, 2f, 1, Day(9, 1), null)], order.DayTimePlaces);
    }

    [Fact]
    public async Task GetOne_NumberedOrder_HasTheNumberAndTheIssueData()
    {
        // Act
        GroupOrderResponse? order = await _repository.GetOne(4);

        // Assert
        Assert.NotNull(order);
        Assert.Equal("26.2", order.Number);
        Assert.Equal(Day(11, 30), order.IssueDate);
        Assert.Equal("Organization", order.OrganizationName);
        Assert.Equal("Director", order.SignerTitle);
        Assert.Equal("Signer", order.SignerName);
        Assert.Null(order.GroupCode);
        Assert.Equal([new GroupOrderTeacherResponse(401, 6, 9, null, null)], order.Teachers);
    }

    [Fact]
    public async Task GetOne_Missing_IsNull()
    {
        Assert.Null(await _repository.GetOne(99));
    }

    [Fact]
    public async Task GetOrder_ReturnsTheOrderForTheEngine()
    {
        // Act
        GroupOrderData? order = await _repository.GetOrder(1);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(new GroupFields(11, "601", 6, 3, 10), order.Fields);
        Assert.Equal([201, 202], order.Students.Select(s => s.Id));
        Assert.Null(await _repository.GetOrder(99));
    }

    //გადაყვანის სამიზნე ჯგუფის კოდი და ჯგუფის ველების სახელები ჯგუფიდან
    [Fact]
    public async Task GetPrintSource_Transfer_HasBothGroupsAndTheGroupNames()
    {
        // Act
        GroupOrderPrintSource? source = await _repository.GetPrintSource(3);

        // Assert
        Assert.NotNull(source);
        Assert.Equal(3, source.Order.Id);
        Assert.Equal(new OrderNumber(26, 1), source.Number);
        Assert.Equal(Day(9, 30), source.IssueDate);
        Assert.Equal("Organization", source.OrganizationName);
        Assert.Equal("Director", source.SignerTitle);
        Assert.Equal("Signer", source.SignerName);
        Assert.Equal("601", source.GroupCode);
        Assert.Equal("602", source.TargetGroupCode);
        Assert.Equal("2026-2027", source.AcademicYearName);
        Assert.Equal("Math", source.CourseName);
        Assert.Equal("2-Pair", source.GroupSizeName);
        Assert.Equal("Tenth", source.StudentStatusName);
    }

    //დაპაუზებულ შექმნის ბრძანებას ჯგუფი არ აქვს: ყველაფერი მისი ველებიდან
    [Fact]
    public async Task GetPrintSource_PausedCreateOrder_TakesItsOwnFields()
    {
        // Act
        GroupOrderPrintSource? source = await _repository.GetPrintSource(5);

        // Assert
        Assert.NotNull(source);
        Assert.Equal("1002", source.GroupCode);
        Assert.Null(source.TargetGroupCode);
        Assert.Equal("4-Four", source.GroupSizeName);
        Assert.Equal("Math", source.CourseName);
        Assert.Null(await _repository.GetPrintSource(99));
    }

    //ადამიანები მხოლოდ მოთხოვნილი კონტრაქტებით, ცნობარები მთლიანად
    [Fact]
    public async Task GetPrintNames_NamesThePeopleAndTheLookups()
    {
        // Act
        GroupOrderPrintNames names = await _repository.GetPrintNames([5], [20]);

        // Assert
        AssertNames([(5, "Delta Dan")], names.Teachers);
        AssertNames([(20, "Beta Bob")], names.Students);
        AssertNames([(8, "Main"), (9, "Hourly")], names.SalarySchemes);
        AssertNames([(1, "Monday"), (3, "Wednesday")], names.WeekDays);
        AssertNames([(2, "09:30"), (4, "15:00")], names.LessonStartTimes);
        AssertNames([(1, "Room 1"), (2, "Room 2")], names.Rooms);
    }

    //სტრიქონის LineId ბაზის სტრიქონის Id-ია (ვიზარდი 1-ის შესავალი)
    [Fact]
    public async Task GetGroupStates_MapsTheStoredRows()
    {
        // Act
        Dictionary<int, GroupState> states = await _repository.GetGroupStates([1, 3, 99]);

        // Assert
        Assert.Equal([1, 3], states.Keys.Order());
        GroupState state = states[1];
        Assert.Equal(new GroupFields(11, "601", 6, 3, 10), state.Fields);
        Assert.Null(state.VoidDate);
        Assert.Equal([
            new GroupTeacherRow(11, 5, 8, Day(9, 1), Day(12, 1)), new GroupTeacherRow(12, 6, 9, Day(12, 1), null)
        ], state.Teachers);
        Assert.Equal([new GroupStudentRow(21, 20, 8f, 48m, 6m, 1f, "note", Day(9, 1), Day(10, 1))], state.Students);
        Assert.Equal([new GroupDayTimePlaceRow(31, 1, 4, 2f, 1, Day(9, 1), null)], state.DayTimePlaces);
        Assert.Empty(states[3].Teachers);
    }

    //D202: ბრძანება შეიძლება ჯგუფს, რომელსაც საწყისი ან გააქტიურებული შექმნის ბრძანება აქვს
    [Fact]
    public async Task GetGroupLookups_ListsTheYearsGroupsByCode()
    {
        // Act
        List<GroupOrderGroupLookupResponse> groups = await _repository.GetGroupLookups(11);

        // Assert
        Assert.Equal([
            new GroupOrderGroupLookupResponse(3, "1001", 11, 6, 2, null, false),
            new GroupOrderGroupLookupResponse(1, "601", 11, 6, 3, null, true),
            new GroupOrderGroupLookupResponse(2, "602", 11, 6, 3, null, true)
        ], groups);
    }

    //D186: წლის ჯგუფებისა და დაპაუზებული შექმნის ბრძანებების კოდები
    [Fact]
    public async Task GetTakenGroupCodes_AreTheYearsGroupAndPausedOrderCodes()
    {
        Assert.Equal(["1001", "1002", "601", "602"], (await _repository.GetTakenGroupCodes(11, null, null)).Order());
        Assert.Equal(["1001", "602"], (await _repository.GetTakenGroupCodes(11, 1, 5)).Order());
        Assert.Equal(["501"], await _repository.GetTakenGroupCodes(10, null, null));
    }

    [Fact]
    public async Task GetStudentStatusRate_ReturnsTheRateOrNull()
    {
        Assert.Equal(110, await _repository.GetStudentStatusRate(10));
        Assert.Null(await _repository.GetStudentStatusRate(99));
    }
}
