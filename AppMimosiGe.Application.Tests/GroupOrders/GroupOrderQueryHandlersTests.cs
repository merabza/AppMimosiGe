using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.GetGroupOrder;
using AppMimosiGe.Application.GroupOrders.GetGroupOrderFormLookups;
using AppMimosiGe.Application.GroupOrders.GetGroupOrderPrint;
using AppMimosiGe.Application.GroupOrders.GetGroupOrders;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersMode;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//query-ების handler-ები mock რეპოზიტორიებით. დღეს 25.11.2026-ია. ბრძანებები _orders-შია, ჯგუფების ბაზის მდგომარეობა
//_storedStates-ში; ძრავა ნამდვილია. ჯგუფი 1 ("601", საგანი 6, ზომა 2) საწყისი ბრძანებით 1 (01.09: მასწავლებელი 5,
//ორშაბათის 2 საათი, მოსწავლე 20) და ბრძანებით 3 (01.12: მოსწავლე 21-ის დამატება)
public sealed class GroupOrderQueryHandlersTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
    private readonly Mock<IGroupsRepository> _groupsRepository = new();
    private readonly List<GroupOrderData> _orders = [];
    private readonly Mock<IGroupOrdersRepository> _repository = new();
    private readonly Dictionary<int, GroupState> _storedStates = [];
    private IReadOnlyCollection<int> _printStudentIds = [];
    private IReadOnlyCollection<int> _printTeacherIds = [];
    private readonly Mock<IStudentContractsRepository> _studentContracts = new();
    private readonly Mock<ITeacherContractsRepository> _teacherContracts = new();

    public GroupOrderQueryHandlersTests()
    {
        _repository.Setup(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            [
                .. _orders.Where(o => o.GroupId is { } groupId && ids.Contains(groupId) ||
                                      o.TargetGroupId is { } targetId && ids.Contains(targetId))
                    .OrderBy(o => o.OrderDate).ThenBy(o => o.Id)
            ]);
        _repository.Setup(r => r.GetOrder(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _orders.Find(o => o.Id == id));
        _repository.Setup(r => r.GetGroupStates(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.Where(_storedStates.ContainsKey).ToDictionary(id => id, id => _storedStates[id]));
        _repository.Setup(r => r.GetNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new OrderNumber(26, id));
        _repository.Setup(r => r.GetStudentStatusRate(10, It.IsAny<CancellationToken>())).ReturnsAsync(110);
        _repository.Setup(r => r.GetTakenGroupCodes(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(["1001", "601"]);
        _repository.Setup(r => r.GetGroupLookups(11, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroupOrderGroupLookupResponse(1, "601", 11, 6, 2, null, true)]);

        _studentContracts.Setup(r => r.GetAcademicYears(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = Day(9, 1), FinishDate = Day(9, 1).AddYears(1)
            },
            new AcademicYear
            {
                AyId = 10, AcademicYearName = "2025-2026", StartDate = Day(9, 1).AddYears(-1), FinishDate = Day(9, 1)
            }
        ]);
        _studentContracts.Setup(r => r.GetCourses(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(6, "მათემატიკა")]);
        _studentContracts.Setup(r => r.GetGroupSizes(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(2, "4-ოთხი"), new LookupItemResponse(3, "2-წყვილი")]);
        _studentContracts.Setup(r => r.GetStudentStatuses(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(10, "მეათე")]);
        _groupsRepository.Setup(r => r.GetStudentContracts(11, It.IsAny<CancellationToken>())).ReturnsAsync([
            Contract(20, new StudentContractDetailResponse(1, 6, 2, 8f, 48m, 6m)),
            Contract(21, new StudentContractDetailResponse(2, 6, 3, 8f, 56m, 7m))
        ]);
        _groupsRepository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, id => $"მოსწავლე {id}"));
        _groupsRepository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroupTeacherContractLookupResponse(5, "მასწავლებელი 5", 8)]);
        _groupsRepository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(1, "ორშაბათი")]);
        _groupsRepository.Setup(r => r.GetLessonStartTimes(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(4, "15:00")]);
        _groupsRepository.Setup(r => r.GetRooms(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(1, "ოთახი 1")]);
        _teacherContracts.Setup(r => r.GetSalarySchemes(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(8, "ძირითადი")]);

        _orders.Add(InitialOrder(1, 1, InitialStudentLine(101, 20)));
        _orders.Add(StudentsOrder(3, EGroupOrderType.AddStudents, Day(12, 1), 1, StudentLine(301, 21)));
        //ბაზის სტრიქონები ბრძანებების შედეგს განზრახ არ ემთხვევა: ასე ჩანს, საიდან მოდის მდგომარეობა
        _storedStates[1] = State(students: [StudentLineRow(91, 22, Day(9, 1), null)]);
    }

    private static TimeProvider TimeProviderAt(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    private static GroupStudentContractLookupResponse Contract(int scId,
        params StudentContractDetailResponse[] tariffs)
    {
        return new GroupStudentContractLookupResponse(scId, $"მოსწავლე {scId}", [.. tariffs]);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    private static GetGroupOrderFormLookupsQuery Lookups(int? academicYearId = null, DateTime? orderDate = null,
        int? grpId = null, int? targetGrpId = null, int? studentStatusId = null, int? courseId = null,
        int? groupSizeId = null, int? orderId = null)
    {
        return new GetGroupOrderFormLookupsQuery(academicYearId, orderDate, grpId, targetGrpId, studentStatusId,
            courseId, groupSizeId, orderId);
    }

    private Task<Result<GroupOrderFormLookupsResponse>> HandleLookups(GetGroupOrderFormLookupsQuery query)
    {
        return new GetGroupOrderFormLookupsQueryHandler(_repository.Object, _groupsRepository.Object,
            _studentContracts.Object, _teacherContracts.Object, TimeProviderAt(Now)).Handle(query,
            CancellationToken.None);
    }

    private static void AssertError(Error expected, Result result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(expected.Code, result.Error.Code);
    }

    private static int[] StudentIds(GroupOrderGroupStateResponse? state)
    {
        Assert.NotNull(state);
        return [.. state.Students.Select(s => s.StudentContractId)];
    }

    [Fact]
    public async Task GetGroupOrders_ValidRequest_PassesTheTypedQueryToTheRepository()
    {
        // Arrange
        var rows = new GroupOrdersRowsDataResponse(0, 0, []);
        GroupOrdersListQuery? passed = null;
        _repository.Setup(r => r.GetRowsData(It.IsAny<GroupOrdersListQuery>(), It.IsAny<CancellationToken>()))
            .Callback((GroupOrdersListQuery q, CancellationToken _) => passed = q).ReturnsAsync(rows);
        string request = Encode(
            """{"offset":10,"rowsCount":10,"filterFields":[{"fieldName":"orderType","value":"2"}],"sortByFields":null}""");

        // Act
        Result<GroupOrdersRowsDataResponse> result = await new GetGroupOrdersQueryHandler(_repository.Object)
            .Handle(new GetGroupOrdersQuery(request), CancellationToken.None);

        // Assert
        Assert.Same(rows, result.Value);
        Assert.NotNull(passed);
        Assert.Equal(10, passed.Offset);
        Assert.Equal(EGroupOrderType.ChangeTeacher, passed.OrderType);
        Assert.Equal(GroupOrdersListQueryFactory.DefaultSortFields, passed.SortFields);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("eyJvZmZzZXQiOg==")]
    [InlineData("bnVsbA==")]
    [InlineData("eyJvZmZzZXQiOjAsInJvd3NDb3VudCI6MCwiZmlsdGVyRmllbGRzIjpudWxsLCJzb3J0QnlGaWVsZHMiOm51bGx9")]
    public async Task GetGroupOrders_InvalidRequest_IsRefusedWithoutReadingTheRows(string request)
    {
        // Act
        Result<GroupOrdersRowsDataResponse> result = await new GetGroupOrdersQueryHandler(_repository.Object)
            .Handle(new GetGroupOrdersQuery(request), CancellationToken.None);

        // Assert
        AssertError(GroupOrderErrors.FilterSortRequestIsInvalid, result);
        _repository.Verify(r => r.GetRowsData(It.IsAny<GroupOrdersListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetGroupOrder_Found_ReturnsIt()
    {
        // Arrange
        var order = new GroupOrderResponse(3, 3, "26.3", Day(11, 20), Day(12, 1), false, 11, 1, null, null, null,
            null, null, null, "ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია", "manager", Day(11, 20), null,
            null, [], [], []);
        _repository.Setup(r => r.GetOne(3, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        // Act
        Result<GroupOrderResponse> result = await new GetGroupOrderQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderQuery(3), CancellationToken.None);

        // Assert
        Assert.Same(order, result.Value);
    }

    [Fact]
    public async Task GetGroupOrder_Missing_IsNotFound()
    {
        // Act
        Result<GroupOrderResponse> result = await new GetGroupOrderQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderQuery(99), CancellationToken.None);

        // Assert
        AssertError(GroupOrderErrors.GroupOrderNotFound, result);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetGroupOrdersMode_ReturnsTheStoredMode()
    {
        // Arrange
        var mode = new GroupOrdersModeResponse(true, Day(11, 24), "admin", Day(11, 24));
        _repository.Setup(r => r.GetMode(It.IsAny<CancellationToken>())).ReturnsAsync(mode);

        // Act
        Result<GroupOrdersModeResponse> result = await new GetGroupOrdersModeQueryHandler(_repository.Object)
            .Handle(new GetGroupOrdersModeQuery(), CancellationToken.None);

        // Assert
        Assert.Same(mode, result.Value);
    }

    [Fact]
    public async Task GetGroupOrderPrint_Missing_IsNotFound()
    {
        // Act
        Result<GroupOrderPrintResponse> result = await new GetGroupOrderPrintQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderPrintQuery(99), CancellationToken.None);

        // Assert
        AssertError(GroupOrderErrors.GroupOrderNotFound, result);
    }

    //D193, D215: საწყისი და დაპაუზებული ბრძანება არ იბეჭდება
    [Theory]
    [InlineData(EGroupOrderType.Initial, false, nameof(GroupOrderErrors.InitialOrderIsNotPrinted))]
    [InlineData(EGroupOrderType.CreateGroup, true, nameof(GroupOrderErrors.PausedOrderIsNotPrinted))]
    public async Task GetGroupOrderPrint_InitialOrPausedOrder_IsRefused(EGroupOrderType orderType, bool isPaused,
        string errorCode)
    {
        // Arrange
        WithPrintSource(Order(7, orderType, Day(12, 1), 1) with { IsPaused = isPaused, Fields = Fields });

        // Act
        Result<GroupOrderPrintResponse> result = await new GetGroupOrderPrintQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderPrintQuery(7), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _repository.Verify(r => r.GetPrintNames(It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    //ძველი მნიშვნელობების გარეშე ბრძანება ჯგუფის ბრძანებებს არ კითხულობს
    [Fact]
    public async Task GetGroupOrderPrint_AddStudents_NeedsNoPreviousState()
    {
        // Arrange
        WithPrintSource(_orders[1]);
        WithPrintNames();

        // Act
        Result<GroupOrderPrintResponse> result = await new GetGroupOrderPrintQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderPrintQuery(3), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("26.3", result.Value.Number);
        Assert.Equal([new GroupOrderPrintStudentResponse("მოსწავლე 21", 8f, 6m, 48m, null, null, null, null)],
            result.Value.Students);
        _repository.Verify(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(_printTeacherIds);
        Assert.Equal([21], _printStudentIds);
    }

    //ძველი მასწავლებელი ჯგუფის მდგომარეობიდანაა ბრძანებამდე, არა ბაზიდან, სადაც მას ბრძანება უკვე დაასრულებს
    [Fact]
    public async Task GetGroupOrderPrint_ChangeTeacher_TakesThePreviousTeacherFromTheStateBeforeTheOrder()
    {
        // Arrange
        GroupOrderData changeTeacher = Order(4, EGroupOrderType.ChangeTeacher, Day(12, 1), 1) with
        {
            Teachers = [new GroupOrderTeacherLine(401, 6, 9)]
        };
        _orders.Add(changeTeacher);
        WithPrintSource(changeTeacher);
        WithPrintNames();

        // Act
        Result<GroupOrderPrintResponse> result = await new GetGroupOrderPrintQueryHandler(_repository.Object)
            .Handle(new GetGroupOrderPrintQuery(4), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(new GroupOrderPrintTeacherResponse("მასწავლებელი 6", "სქემა 9"), result.Value.Teacher);
        Assert.Equal(new GroupOrderPrintTeacherResponse("მასწავლებელი 5", "სქემა 8"), result.Value.PreviousTeacher);
        Assert.Equal([6, 5], _printTeacherIds);
        Assert.Empty(_printStudentIds);
    }

    [Fact]
    public async Task GetFormLookups_WithoutParameters_ReturnsTheListsOnly()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups());

        // Assert
        Assert.True(result.IsSuccess);
        GroupOrderFormLookupsResponse lookups = result.Value;
        Assert.Equal(11, lookups.CurrentAcademicYearId);
        Assert.Equal([new LookupItemResponse(10, "2025-2026"), new LookupItemResponse(11, "2026-2027")],
            lookups.AcademicYears);
        Assert.Equal([new LookupItemResponse(6, "მათემატიკა")], lookups.Courses);
        Assert.Equal(2, lookups.GroupSizes.Count);
        Assert.Equal([new LookupItemResponse(10, "მეათე")], lookups.StudentStatuses);
        Assert.Equal([new GroupTeacherContractLookupResponse(5, "მასწავლებელი 5", 8)], lookups.TeacherContracts);
        Assert.Equal([new LookupItemResponse(8, "ძირითადი")], lookups.SalarySchemes);
        Assert.Equal([new LookupItemResponse(1, "ორშაბათი")], lookups.WeekDays);
        Assert.Equal([new LookupItemResponse(4, "15:00")], lookups.LessonStartTimes);
        Assert.Equal([new LookupItemResponse(1, "ოთახი 1")], lookups.Rooms);
        Assert.Empty(lookups.Groups);
        Assert.Empty(lookups.StudentContracts);
        Assert.Empty(lookups.StudentTariffs);
        Assert.Null(lookups.Group);
        Assert.Null(lookups.TargetGroup);
        Assert.Null(lookups.SuggestedGroupCode);
        _repository.Verify(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetFormLookups_AcademicYear_ReturnsItsGroupsAndContracts()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([new GroupOrderGroupLookupResponse(1, "601", 11, 6, 2, null, true)], result.Value.Groups);
        Assert.Equal([20, 21], result.Value.StudentContracts.Select(c => c.ScId));
        Assert.Empty(result.Value.StudentTariffs);
    }

    //ახალი ბრძანების მდგომარეობა ბრძანებებიდან ითვლება, მისი დღის ყველა ბრძანებით, ბაზის სტრიქონებიდან კი არა
    [Fact]
    public async Task GetFormLookups_Group_IsTheStateOfItsOrdersOnTheOrderDate()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, Day(12, 1), 1));

        // Assert
        Assert.True(result.IsSuccess);
        GroupOrderGroupStateResponse? group = result.Value.Group;
        Assert.Equal([20, 21], StudentIds(group));
        Assert.Equal("მოსწავლე 21", group!.Students[1].StudentContractName);
        Assert.Equal(Day(9, 1), group.StartDate);
        Assert.True(group.CanHaveOrders);
        Assert.True(group.IsActive);
        Assert.Equal(2f, group.WeeklyHours);
        Assert.Null(result.Value.TargetGroup);
    }

    //შესაცვლელი ბრძანება მდგომარეობაში არ ითვლება
    [Fact]
    public async Task GetFormLookups_EditedOrder_IsLeftOutOfTheState()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, Day(12, 1), 1, orderId: 3));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([20], StudentIds(result.Value.Group));
    }

    //ბრძანებების გარეშე ჯგუფის მდგომარეობა ბაზისაა; მას ბრძანება ვერ შეექმნება (D202)
    [Fact]
    public async Task GetFormLookups_GroupWithoutOrders_IsItsStoredStateAndCannotHaveOrders()
    {
        // Arrange
        _storedStates[2] = State(students: [StudentLineRow(92, 30, Day(9, 1), null)]);

        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(orderDate: Day(12, 1), grpId: 2));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([30], StudentIds(result.Value.Group));
        Assert.False(result.Value.Group!.CanHaveOrders);
        Assert.Null(result.Value.Group.StartDate);
    }

    //ჯგუფი, რომელიც ბრძანების თარიღზე ჯერ არ დაწყებულა, სტრიქონების გარეშეა, ველები და დაწყება კი ჩანს
    [Fact]
    public async Task GetFormLookups_BeforeTheGroupStart_HasNoRows()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(orderDate: Day(8, 31), grpId: 1));

        // Assert
        Assert.True(result.IsSuccess);
        GroupOrderGroupStateResponse? group = result.Value.Group;
        Assert.NotNull(group);
        Assert.Equal("601", group.GroupCode);
        Assert.Equal(Day(9, 1), group.StartDate);
        Assert.False(group.IsActive);
        Assert.Empty(group.Teachers);
        Assert.Empty(group.Students);
        Assert.Empty(group.DayTimePlaces);
    }

    //შესაცვლელი ბრძანების გადატანისას გვიანდელი ბრძანება აღარ ესადაგება: შეცდომა იგივეა, რაც შენახვისას (D185)
    [Fact]
    public async Task GetFormLookups_MovedOrderMakesALaterOneUnfit_ReturnsItsNumber()
    {
        // Arrange
        _orders.Add(StudentsOrder(4, EGroupOrderType.RemoveStudents, Day(12, 10), 1, RemovalLine(401, 21)));

        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, Day(12, 15), 1, orderId: 3));

        // Assert
        AssertError(GroupOrderErrors.OrderNoLongerFits("26.4"), result);
        Assert.Contains("26.4", result.Error.Description, StringComparison.Ordinal);
    }

    //D218: სხვა ჯგუფის ბრძანებაში (3) ჯგუფის საგნისა და ზომის დეტალი
    [Fact]
    public async Task GetFormLookups_Group_DefaultTariffsUseItsCourseAndSize()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, Day(12, 1), 1));

        // Assert
        Assert.Equal([new GroupOrderStudentTariffResponse(20, 8f, 48m, 6m, 1f)], result.Value.StudentTariffs);
    }

    //D218: გადაყვანისას სამიზნის საგნისა და ზომის დეტალი, თორემ „საიდან" ჯგუფის სტრიქონის ტარიფი
    [Fact]
    public async Task GetFormLookups_Transfer_DefaultTariffsUseTheTargetOrTheSourceRow()
    {
        // Arrange
        _orders.Add(InitialOrder(2, 2) with { Fields = Fields with { GroupCode = "602", GroupSizeId = 3 } });
        _storedStates[2] = State();

        // Act
        Result<GroupOrderFormLookupsResponse> result =
            await HandleLookups(Lookups(11, Day(11, 20), 1, 2));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal([
            new GroupOrderStudentTariffResponse(20, 8f, 48m, 6m, 1f),
            new GroupOrderStudentTariffResponse(21, 8f, 56m, 7m, 1f)
        ], result.Value.StudentTariffs);
        Assert.Equal("602", result.Value.TargetGroup!.GroupCode);
        Assert.Empty(result.Value.TargetGroup.Students);
    }

    //D218: შექმნის ბრძანებაში მოთხოვნის საგანი და ზომა
    [Fact]
    public async Task GetFormLookups_CourseAndSize_DefaultTariffsForTheCreateOrder()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, courseId: 6, groupSizeId: 3));

        // Assert
        Assert.Equal([new GroupOrderStudentTariffResponse(21, 8f, 56m, 7m, 1f)], result.Value.StudentTariffs);
    }

    //D186: სტატუსის Rate 110 კლასი 10-ია; 1001 დაკავებულია
    [Fact]
    public async Task GetFormLookups_YearAndStatus_SuggestTheFirstFreeCode()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(11, studentStatusId: 10));

        // Assert
        Assert.Equal("1002", result.Value.SuggestedGroupCode);
        _repository.Verify(r => r.GetTakenGroupCodes(11, null, null, It.IsAny<CancellationToken>()));
    }

    //შესაცვლელი შექმნის ბრძანების და მისი ჯგუფის კოდი თავისუფლად ითვლება, როგორც შენახვისას (GroupCodeIsTaken)
    [Fact]
    public async Task GetFormLookups_EditedCreateOrder_FreesItsOwnCode()
    {
        // Act
        await HandleLookups(Lookups(11, studentStatusId: 10, orderId: 1));

        // Assert
        _repository.Verify(r => r.GetTakenGroupCodes(11, 1, 1, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task GetFormLookups_EditedOtherOrder_FreesNoGroupCode()
    {
        // Act
        await HandleLookups(Lookups(11, studentStatusId: 10, orderId: 3));

        // Assert
        _repository.Verify(r => r.GetTakenGroupCodes(11, null, 3, It.IsAny<CancellationToken>()));
    }

    //სტატუსი მოწმდება, მაგრამ წლის გარეშე შეთავაზება არ არის
    [Fact]
    public async Task GetFormLookups_StatusWithoutYear_SuggestsNothing()
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(Lookups(studentStatusId: 10));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SuggestedGroupCode);
        _repository.Verify(r => r.GetTakenGroupCodes(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<GetGroupOrderFormLookupsQuery, string> InvalidParameters =>
        new()
        {
            { Lookups(grpId: 1), nameof(GroupOrderErrors.OrderDateIsRequired) },
            { Lookups(targetGrpId: 1), nameof(GroupOrderErrors.OrderDateIsRequired) },
            { Lookups(99), nameof(GroupErrors.AcademicYearNotFound) },
            { Lookups(courseId: 99), nameof(GroupErrors.CourseNotFound) },
            { Lookups(groupSizeId: 99), nameof(GroupErrors.GroupSizeNotFound) },
            { Lookups(studentStatusId: 99), nameof(GroupErrors.StudentStatusNotFound) },
            { Lookups(orderId: 99), nameof(GroupOrderErrors.GroupOrderNotFound) },
            { Lookups(orderDate: Day(12, 1), grpId: 99), nameof(GroupOrderErrors.GroupNotFound) },
            { Lookups(orderDate: Day(12, 1), grpId: 1, targetGrpId: 99), nameof(GroupOrderErrors.TargetGroupNotFound) }
        };

    //query-ის პარამეტრები handler-ში მოწმდება (ValidationDecorator მხოლოდ command-ებს ამოწმებს)
    [Theory]
    [MemberData(nameof(InvalidParameters))]
    public async Task GetFormLookups_InvalidParameter_IsRefused(GetGroupOrderFormLookupsQuery query,
        string errorCode)
    {
        // Act
        Result<GroupOrderFormLookupsResponse> result = await HandleLookups(query);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(errorCode, result.Error.Code);
    }

    private void WithPrintSource(GroupOrderData order)
    {
        _repository.Setup(r => r.GetPrintSource(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(
            new GroupOrderPrintSource(order,
                order.OrderType == EGroupOrderType.Initial ? null : new OrderNumber(26, order.Id), Day(11, 20), "ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია", "601", null, "2026-2027",
                "მათემატიკა", "4-ოთხი", "მეათე"));
    }

    private void WithPrintNames()
    {
        _repository.Setup(r => r.GetPrintNames(It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<int> teacherIds, IReadOnlyCollection<int> studentIds,
                CancellationToken _) =>
            {
                _printTeacherIds = teacherIds;
                _printStudentIds = studentIds;
            })
            .ReturnsAsync((IReadOnlyCollection<int> teacherIds, IReadOnlyCollection<int> studentIds,
                CancellationToken _) => new GroupOrderPrintNames(
                teacherIds.ToDictionary(id => id, id => $"მასწავლებელი {id}"),
                new Dictionary<int, string> { [8] = "სქემა 8", [9] = "სქემა 9" },
                studentIds.ToDictionary(id => id, id => $"მოსწავლე {id}"),
                new Dictionary<int, string> { [1] = "ორშაბათი" }, new Dictionary<int, string> { [4] = "15:00" },
                new Dictionary<int, string> { [1] = "ოთახი 1" }));
    }
}
