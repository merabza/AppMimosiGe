using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.DeleteGroupOrder;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.GroupOrders.UpdateGroupOrder;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using AppMimosiGe.Application.Tests.Groups;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrderRequests;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//handler-ები mock რეპოზიტორიებით. დღეს ოთხშაბათია, 25.11.2026, გადასვლის დღე (D0) 24.11.2026. ბრძანებები და ჯგუფები
//_orders-ში, _entities-ში და _groups-შია, როგორც ბაზაში; ძრავა, წესები და ჩაწერა ნამდვილია
public sealed class GroupOrderHandlersTests
{
    private const int AddedOrderId = 42;
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime TransitionDay = Day(11, 24);
    private readonly List<Group> _addedGroups = [];
    private readonly Mock<IUserClaimRights> _claimRights = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Dictionary<int, GroupOrder> _entities = [];
    private readonly Dictionary<int, Group> _groups = [];
    private readonly Mock<IGroupsRepository> _groupsRepository = GroupTestData.RepositoryWhereEverythingExists();
    private readonly Mock<ILessonGeneratorRepository> _lessonGenerator = new();
    private readonly List<int> _markedContracts = [];
    private readonly List<GroupOrderData> _orders = [];
    private readonly List<GroupRemovedRows> _removedRows = [];
    private readonly Mock<IGroupOrdersRepository> _repository = new();

    private readonly Mock<IStudentContractsRepository> _studentContracts =
        GroupTestData.StudentContractsRepositoryWhereEverythingExists();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private GroupOrder? _addedOrder;

    public GroupOrderHandlersTests()
    {
        WithOrdersMode(true);
        _repository.Setup(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            [
                .. _orders.Where(o => o.GroupId is { } groupId && ids.Contains(groupId) ||
                                      o.TargetGroupId is { } targetId && ids.Contains(targetId))
                    .OrderBy(o => o.OrderDate).ThenBy(o => o.Id)
            ]);
        _repository.Setup(r => r.GetOrderForChange(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _entities.GetValueOrDefault(id));
        _repository.Setup(r =>
                r.GetGroupsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                [.. ids.Where(_groups.ContainsKey).Select(id => _groups[id])]);
        _repository.Setup(r => r.GroupExists(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repository.Setup(r => r.NextNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int numberYear, CancellationToken _) => new OrderNumber(numberYear, 5));
        _repository.Setup(r => r.GetNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new OrderNumber(26, id));
        _repository.Setup(r =>
                r.GetStudentContractsForChange(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
            {
                _markedContracts.AddRange(ids);
                return [.. ids.Select(id => new StudentContract { ScId = id, ContractNumber = $"6.0{id}" })];
            });
        _repository.Setup(r => r.AddOrder(It.IsAny<GroupOrder>())).Callback((GroupOrder o) => _addedOrder = o);
        _repository.Setup(r => r.AddGroup(It.IsAny<Group>())).Callback((Group g) => _addedGroups.Add(g));
        _repository.Setup(r => r.RemoveRows(It.IsAny<GroupRemovedRows>(), It.IsAny<CancellationToken>()))
            .Callback((GroupRemovedRows rows, CancellationToken _) => _removedRows.Add(rows))
            .Returns(Task.CompletedTask);
        //SaveChanges ახალ ბრძანებას Id-ს აძლევს
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _addedOrder?.Id = AddedOrderId).Returns(Task.CompletedTask);

        _studentContracts.Setup(r => r.GetAcademicYear(11, It.IsAny<CancellationToken>())).ReturnsAsync(
            new AcademicYear
            {
                AyId = 11, AcademicYearName = "2026-2027", StartDate = Day(9, 1), FinishDate = Day(9, 1).AddYears(1)
            });
        _groupsRepository.Setup(r => r.GetLessonStartTimesById(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, TimeOnly> { [4] = new(15, 0) });
        _groupsRepository.Setup(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _groupsRepository.Setup(r => r.GetStudentSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _groupsRepository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, id => $"მოსწავლე {id}"));
        _groupsRepository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GroupTeacherContractLookupResponse(5, "მასწავლებელი 5", 8)]);
        _groupsRepository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(1, "ორშაბათი")]);
        _lessonGenerator.Setup(r => r.GetLastOperationMonth(It.IsAny<CancellationToken>())).ReturnsAsync(Day(12, 1));
        _lessonGenerator.Setup(r => r.GetGroupForGeneration(It.IsAny<int>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, bool _, CancellationToken _) => _groups[id]);
        _currentUser.Setup(u => u.Name).Returns("manager");

        //ჯგუფი 3 ("601") საწყისი ბრძანებით 3: მასწავლებელი 5 და ორშაბათის 2 საათი 01.09-იდან, მოსწავლე 20
        SeedGroup(InitialGroupOrder(3, "601", studentContractIds: [20]));
    }

    private static TimeProvider TimeProviderAt(DateTimeOffset now)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(now);
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    private GroupRequestValidator GroupValidator()
    {
        return
        [
            with(_groupsRepository.Object, _studentContracts.Object,
                GroupTestData.TeacherContractsRepositoryWhereEverythingExists().Object)
        ];
    }

    private CreateGroupOrderCommandHandler CreateHandler()
    {
        return new CreateGroupOrderCommandHandler(_repository.Object, _groupsRepository.Object,
            _studentContracts.Object, GroupValidator(), _lessonGenerator.Object, _unitOfWork.Object,
            _claimRights.Object, _currentUser.Object, TimeProviderAt(Now), new GroupOrdersSettings());
    }

    private UpdateGroupOrderCommandHandler UpdateHandler()
    {
        return new UpdateGroupOrderCommandHandler(_repository.Object, _groupsRepository.Object, GroupValidator(),
            _lessonGenerator.Object, _unitOfWork.Object, _claimRights.Object, _currentUser.Object,
            TimeProviderAt(Now));
    }

    private DeleteGroupOrderCommandHandler DeleteHandler()
    {
        return new DeleteGroupOrderCommandHandler(_repository.Object, _groupsRepository.Object, GroupValidator(),
            _lessonGenerator.Object, _unitOfWork.Object, _claimRights.Object, TimeProviderAt(Now));
    }

    private Task<Result<int>> Create(GroupOrderRequest request)
    {
        return CreateHandler().Handle(new CreateGroupOrderCommand(request), CancellationToken.None);
    }

    private Task<Result> Update(int orderId, GroupOrderRequest request)
    {
        return UpdateHandler().Handle(new UpdateGroupOrderCommand(orderId, request), CancellationToken.None);
    }

    private Task<Result> Delete(int orderId)
    {
        return DeleteHandler().Handle(new DeleteGroupOrderCommand(orderId), CancellationToken.None);
    }

    private void WithOrdersMode(bool isOrdersMode)
    {
        _repository.Setup(r => r.GetMode(It.IsAny<CancellationToken>())).ReturnsAsync(
            new GroupOrdersModeResponse(isOrdersMode, isOrdersMode ? TransitionDay : null, null, null));
    }

    private void WithPastOrdersRight()
    {
        _claimRights.Setup(r => r.HasClaim(GroupOrderClaims.ChangePastGroupOrders, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    //საწყისი ბრძანება (Id = ჯგუფის Id) 01.09-იდან: მასწავლებელი (ხაზი groupId*10+1), ორშაბათის 2 საათი (ხაზი
    //groupId*10+2, scheduleEnd-მდე) და მოსწავლეები (ხაზები groupId*10+3, …), 8 საათი 6 ლარად
    private static GroupOrderData InitialGroupOrder(int groupId, string groupCode, int teacherContractId = 5,
        DateTime? scheduleEnd = null, int[]? studentContractIds = null)
    {
        return new GroupOrderData
        {
            Id = groupId,
            OrderType = EGroupOrderType.Initial,
            OrderDate = Day(9, 1),
            GroupId = groupId,
            Fields = new GroupFields(11, groupCode, 6, 2, 10),
            Teachers = [new GroupOrderTeacherLine(groupId * 10 + 1, teacherContractId, 8, Day(9, 1))],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(groupId * 10 + 2, 1, 4, 2f, 1, Day(9, 1), scheduleEnd)],
            Students =
            [
                .. (studentContractIds ?? []).Select((sc, i) => InitialStudentLine(groupId * 10 + 3 + i, sc))
            ]
        };
    }

    //შექმნის ბრძანება (Id = ჯგუფის Id) orderDate-იდან: მასწავლებელი 5, ორშაბათის 2 საათი და მოსწავლე 30
    private static GroupOrderData CreateGroupOrder(int groupId, string groupCode, DateTime orderDate)
    {
        return new GroupOrderData
        {
            Id = groupId,
            OrderType = EGroupOrderType.CreateGroup,
            OrderDate = orderDate,
            GroupId = groupId,
            Fields = new GroupFields(11, groupCode, 6, 2, 10),
            Teachers = [new GroupOrderTeacherLine(groupId * 10 + 1, 5, 8)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(groupId * 10 + 2, 1, 4, 2f, 1)],
            Students = [StudentLine(groupId * 10 + 3, 30)]
        };
    }

    //ბრძანება ბაზაში: ძრავისთვის (_orders) და შესაცვლელად (_entities, ხაზებით და ნომრით)
    private GroupOrder SeedOrder(GroupOrderData data)
    {
        if (!data.IsPaused)
        {
            _orders.Add(data);
        }

        var order = new GroupOrder
        {
            Id = data.Id,
            AcademicYearId = 11,
            NumberYear = data.OrderType == EGroupOrderType.Initial ? null : 26,
            Number = data.OrderType == EGroupOrderType.Initial ? null : data.Id,
            CreatedByUserName = "manager"
        };
        (GroupOrderNewLines newLines, _) = GroupOrderEntityMapper.Write(order, data, []);
        foreach ((int id, GroupOrderTeacher line) in newLines.Teachers)
        {
            line.Id = id;
        }

        foreach ((int id, GroupOrderStudent line) in newLines.Students)
        {
            line.Id = id;
        }

        foreach ((int id, GroupOrderDayTimePlace line) in newLines.DayTimePlaces)
        {
            line.Id = id;
        }

        _entities[data.Id] = order;
        return order;
    }

    //ჯგუფი ბაზაში თავისი საწყისი ან შექმნის ბრძანების შედეგით: სტრიქონი ხაზს უკავშირდება, Id = ხაზის Id × 10
    private void SeedGroup(GroupOrderData creatingOrder)
    {
        SeedOrder(creatingOrder);
        GroupState state = GroupOrderEffects.Apply(null, creatingOrder).Value;
        var group = new Group { GrpId = creatingOrder.GroupId!.Value, GroupCode = state.Fields.GroupCode };
        GroupStateWriter.Apply(group, state, GroupOrderNewLines.None);
        foreach (GroupByTeacher teacher in group.GroupsByTeachers)
        {
            teacher.Id = teacher.GroupOrderTeacherId!.Value * 10;
        }

        foreach (GroupByStudent student in group.GroupsByStudents)
        {
            student.GbsId = student.GroupOrderStudentId!.Value * 10;
        }

        foreach (GroupDayTimePlace dayTimePlace in group.GroupDayTimePlaces)
        {
            dayTimePlace.GdtpId = dayTimePlace.GroupOrderDayTimePlaceId!.Value * 10;
            dayTimePlace.LessonStartTime = new LessonStartTime { LstId = 4, LstTime = new TimeOnly(15, 0) };
        }

        _groups[group.GrpId] = group;
    }

    private void VerifySaved(Times times)
    {
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), times);
    }

    //D183: ჯგუფების რეჟიმში ბრძანება არ ინახება
    [Fact]
    public async Task Create_InGroupsMode_IsConflictAndNothingIsWritten()
    {
        WithOrdersMode(false);

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21)));

        Assert.Equal(nameof(GroupOrderErrors.GroupOrdersAreNotEnabled), result.Error.Code);
        Assert.Null(_addedOrder);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task UpdateAndDelete_InGroupsMode_AreConflicts()
    {
        WithOrdersMode(false);

        Result updated = await Update(3, StudentsRequest(EGroupOrderType.Initial, 3, Day(9, 1)));
        Result deleted = await Delete(3);

        Assert.Equal(nameof(GroupOrderErrors.GroupOrdersAreNotEnabled), updated.Error.Code);
        Assert.Equal(nameof(GroupOrderErrors.GroupOrdersAreNotEnabled), deleted.Error.Code);
        _repository.Verify(r => r.GetOrderForChange(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySaved(Times.Never());
    }

    //ბრძანება 3: მოსწავლის ახალი სტრიქონი ახალ ხაზს უკავშირდება, ბრძანებას ნომერი და გამოცემის მონაცემები ეწერება
    //(D192, D193), ჯგუფს DirtyLessons, მოსწავლეებს შემდეგი გადახდის თარიღის გადათვლა (D198)
    [Fact]
    public async Task Create_AddStudents_WritesTheRowsAndAnIssuedNumberedOrder()
    {
        Result<int> result =
            await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21, note: "ახალი")));

        Assert.True(result.IsSuccess);
        Assert.Equal(AddedOrderId, result.Value);
        GroupOrder order = Assert.IsType<GroupOrder>(_addedOrder);
        Assert.Equal((EGroupOrderType.AddStudents, Day(12, 1), 3, 11, false),
            (order.OrderType, order.OrderDate, order.GroupId!.Value, order.AcademicYearId, order.IsPaused));
        Assert.Equal((26, 5, Day(11, 25)), (order.NumberYear!.Value, order.Number!.Value, order.IssueDate!.Value));
        Assert.Equal(("ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია"),
            (order.OrganizationName, order.SignerTitle, order.SignerName));
        Assert.Equal(("manager", Now.DateTime), (order.CreatedByUserName, order.CreatedAt));
        GroupOrderStudent line = Assert.Single(order.GroupOrderStudents);
        Assert.Equal((21, 8f, "ახალი"), (line.StudentContractId, line.FourWeekHours!.Value, line.Note));

        Group group = _groups[3];
        GroupByStudent row = Assert.Single(group.GroupsByStudents, s => s.StudentContractId == 21);
        Assert.Same(line, row.GroupOrderStudent);
        Assert.Equal((Day(12, 1), 8f, 48m), (row.StartDate, row.FourWeekHours, row.FourWeekFee));
        Assert.True(group.DirtyLessons);
        Assert.Equal([20, 21], _markedContracts.Distinct().Order());
        _repository.Verify(r => r.NextNumber(26, It.IsAny<CancellationToken>()), Times.Once);
        VerifySaved(Times.Once());
    }

    //D184: Manager-ს წარსული თარიღი არ შეუძლია; ჯგუფი არ იტვირთება
    [Fact]
    public async Task Create_PastDateWithoutRight_IsRefusedBeforeTheReplay()
    {
        Result<int> result =
            await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(11, 24), Student(21)));

        Assert.Equal(nameof(GroupOrderErrors.PastOrderNeedsRight), result.Error.Code);
        _repository.Verify(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_PastDateWithRight_IsSaved()
    {
        WithPastOrdersRight();

        Result<int> result =
            await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(11, 2), Student(21)));

        Assert.True(result.IsSuccess);
        Assert.Contains(_groups[3].GroupsByStudents, s => s is { StudentContractId: 21 } && s.StartDate == Day(11, 2));
        VerifySaved(Times.Once());
    }

    //D196: საწყის ბრძანებას მხოლოდ ბრძანებებზე გადასვლა ქმნის
    [Fact]
    public async Task Create_InitialOrder_IsCreatedByTheTransitionOnly()
    {
        var request = new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.Initial,
            OrderDate = Day(12, 1),
            AcademicYearId = 11,
            GroupCode = "609",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10
        };

        Result<int> result = await Create(request);

        Assert.Equal(nameof(GroupOrderErrors.InitialOrderIsCreatedByTransitionOnly), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D202: ჯგუფს არც საწყისი ბრძანება აქვს და არც შექმნის
    [Fact]
    public async Task Create_ForGroupWithoutInitialOrCreateOrder_IsRefused()
    {
        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 9, Day(12, 1), Student(21)));

        Assert.Equal(nameof(GroupOrderErrors.GroupCannotHaveOrders), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D185: შესანახი ბრძანება თვითონ არ ესადაგება: ბრუნდება მიზეზი
    [Fact]
    public async Task Create_OrderThatDoesNotFit_ReturnsItsReason()
    {
        Result<int> result = await Create(StudentsRequest(EGroupOrderType.RemoveStudents, 3, Day(12, 1), Removal(21)));

        Assert.Equal(nameof(GroupOrderErrors.StudentIsNotInGroup), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D185: ადრეული ბრძანების შემდეგ გვიანდელი აღარ ესადაგება: შეცდომა მის ნომერს ასახელებს
    [Fact]
    public async Task Create_EarlierOrderBreakingALaterOne_NamesTheLaterOrder()
    {
        SeedOrder(StudentsOrder(7, EGroupOrderType.RemoveStudents, Day(12, 10), 3, RemovalLine(71, 20)));

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.RemoveStudents, 3, Day(12, 1), Removal(20)));

        Assert.Equal(nameof(GroupOrderErrors.OrderNoLongerFits), result.Error.Code);
        Assert.Contains("№ 26.7", result.Error.Description, StringComparison.Ordinal);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Create_LineWithAnExistingId_IsLineNotFound()
    {
        Result<int> result =
            await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21, id: 33)));

        Assert.Equal(nameof(GroupOrderErrors.LineNotFound), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D186: მასწავლებლის გარეშე შექმნის ბრძანება დაპაუზებულია: ჯგუფს არ ქმნის, ნომერს კი იღებს (D192)
    [Fact]
    public async Task Create_CreateOrderWithoutTeacher_IsSavedPausedWithANumber()
    {
        Result<int> result = await Create(CreateGroupRequest(Day(12, 1), [], [Student(21)], [Monday()]));

        Assert.True(result.IsSuccess);
        GroupOrder order = Assert.IsType<GroupOrder>(_addedOrder);
        Assert.True(order.IsPaused);
        Assert.Null(order.GroupId);
        Assert.Equal(("605", 11, 5), (order.GroupCode, order.AcademicYearId, order.Number!.Value));
        Assert.Equal(8f, Assert.Single(order.GroupOrderStudents).FourWeekHours);
        Assert.Empty(_addedGroups);
        _repository.Verify(r => r.GroupCodeIsTaken(11, "605", null, null, It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Once());
    }

    //D184, D210: დაპაუზებული ბრძანება წარსული თარიღით მხოლოდ უფლებით ინახება
    [Fact]
    public async Task Create_PausedCreateOrderInThePast_IsRefused()
    {
        Result<int> result = await Create(CreateGroupRequest(Day(11, 20), [], [Student(21)], [Monday()]));

        Assert.Equal(nameof(GroupOrderErrors.PausedOrderDateIsInPast), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D186: შექმნის ბრძანება ახალ ჯგუფს ქმნის; ბრძანება ჯგუფს navigation-ით უკავშირდება
    [Fact]
    public async Task Create_CreateOrder_CreatesTheGroup()
    {
        Result<int> result =
            await Create(CreateGroupRequest(Day(11, 30), [Teacher()], [Student(21)], [Monday()]));

        Assert.True(result.IsSuccess);
        Group group = Assert.Single(_addedGroups);
        GroupOrder order = Assert.IsType<GroupOrder>(_addedOrder);
        Assert.Same(group, order.Group);
        Assert.Null(order.GroupId);
        Assert.False(order.IsPaused);
        Assert.Equal(("605", 6, 11), (group.GroupCode, group.CourseId, group.AcademicYearId));
        Assert.Same(order.GroupOrderTeachers.Single(), Assert.Single(group.GroupsByTeachers).GroupOrderTeacher);
        Assert.Same(order.GroupOrderStudents.Single(), Assert.Single(group.GroupsByStudents).GroupOrderStudent);
        Assert.Same(order.GroupOrderDayTimePlaces.Single(),
            Assert.Single(group.GroupDayTimePlaces).GroupOrderDayTimePlace);
        Assert.True(group.DirtyLessons);
        VerifySaved(Times.Once());
    }

    //D186: კოდი წელში უნიკალურია, დაპაუზებული ბრძანებების კოდების ჩათვლით
    [Fact]
    public async Task Create_TakenCode_IsRefused()
    {
        _repository.Setup(r => r.GroupCodeIsTaken(11, "605", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<int> result =
            await Create(CreateGroupRequest(Day(11, 30), [Teacher()], [Student(21)], [Monday()]));

        Assert.Equal(nameof(GroupOrderErrors.GroupCodeIsTaken), result.Error.Code);
        Assert.Empty(_addedGroups);
        VerifySaved(Times.Never());
    }

    //D187, D194: ფასის ფორმულა მოწმდება გადასვლის დღიდან; აღწერა მოსწავლეს ასახელებს
    [Fact]
    public async Task Create_FeeNotMatchingTheHours_IsRefused()
    {
        GroupOrderStudentRequest student = Student(21);
        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1),
            new GroupOrderStudentRequest
            {
                StudentContractId = 21,
                FourWeekFee = 50,
                OneHourFee = student.OneHourFee,
                HoursCoefficient = student.HoursCoefficient
            }));

        Assert.Equal(nameof(GroupOrderErrors.StudentFeeDoesNotMatchHours), result.Error.Code);
        Assert.Contains("მოსწავლე 21 — ჯგუფი 601, 01.12.2026-დან", result.Error.Description,
            StringComparison.Ordinal);
        VerifySaved(Times.Never());
    }

    //ჯგუფის რედაქტორის ვალიდატორები სამიზნე მდგომარეობაზე: განრიგის გარეშე ფორმულა 0 საათს იძლევა (D55: > 0)
    [Fact]
    public async Task Create_StateBreakingAGroupRule_IsAValidationError()
    {
        SeedGroup(InitialGroupOrder(4, "602", scheduleEnd: Day(11, 2), studentContractIds: [22]));

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 4, Day(12, 1), Student(21)));

        ValidationError error = Assert.IsType<ValidationError>(result.Error);
        Assert.Contains(error.Errors, e => e.Code == nameof(GroupErrors.FourWeekHoursMustBePositive));
        VerifySaved(Times.Never());
    }

    //მასწავლებელი 5 ჯგუფ 7-ში ორშაბათს 15:30–17:00 ასწავლის 01.09-იდან end-მდე
    private void WithOtherGroupOfTeacher5(DateTime end)
    {
        _groupsRepository.Setup(r => r.GetTeacherSchedules(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>())).ReturnsAsync([
            new GroupSchedule(7, "1002", null, [new GroupScheduleMember(5, Day(9, 1), null)],
                [new GroupScheduleDayTime(1, new TimeOnly(15, 30), 1.5f, Day(9, 1), end)],
                [new GroupSchedulePeriod(Day(9, 1), null)])
        ]);
    }

    //D212: ბრძანებების რეჟიმში გადაფარვა, რომლის დღეები გადასვლის დღესაც ეხება, ბლოკავს, თუნდაც დღემდე დასრულდა
    [Fact]
    public async Task Create_TeacherOverlapEndedAfterTheTransitionDay_IsRefused()
    {
        WithOtherGroupOfTeacher5(Day(11, 25));

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21)));

        Assert.Equal(nameof(GroupErrors.TeacherTimesOverlap), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D212: გადასვლის დღემდე დასრულებული ისტორია არ ბლოკავს
    [Fact]
    public async Task Create_TeacherOverlapEndedBeforeTheTransitionDay_IsSaved()
    {
        WithOtherGroupOfTeacher5(TransitionDay);

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21)));

        Assert.True(result.IsSuccess);
        VerifySaved(Times.Once());
    }

    //D178, D179: შეხებული ჯგუფები ერთმანეთს სამიზნე მდგომარეობით ედრება. ბაზაში მოსწავლე 20 ჯგუფ 3-ში ჯერ ისევ არის
    //იმავე დროს, როცა ჯგუფ 4-ში იწყებს, გადაყვანის შემდეგ კი იქ 01.12-ს სრულდება
    [Fact]
    public async Task Create_Transfer_ComparesTheTouchedGroupsByTheirTargetState()
    {
        SeedGroup(InitialGroupOrder(4, "602", 6, studentContractIds: [22]));
        _groupsRepository.Setup(r => r.GetStudentSchedules(It.IsAny<IReadOnlyCollection<int>>(), 4,
            It.IsAny<CancellationToken>())).ReturnsAsync([
            new GroupSchedule(3, "601", null, [new GroupScheduleMember(20, Day(9, 1), null)],
                [new GroupScheduleDayTime(1, new TimeOnly(15, 0), 2f, Day(9, 1), null)],
                [new GroupSchedulePeriod(Day(9, 1), null)])
        ]);

        Result<int> result = await Create(TransferRequest(3, 4, Day(12, 1), Student(20)));

        Assert.True(result.IsSuccess);
        Assert.Equal(Day(12, 1), Assert.Single(_groups[3].GroupsByStudents).EndDate);
        GroupByStudent moved = Assert.Single(_groups[4].GroupsByStudents, s => s.StudentContractId == 20);
        Assert.Equal(Day(12, 1), moved.StartDate);
        Assert.Null(moved.EndDate);
        _groupsRepository.Verify(r => r.GetStudentSchedules(It.IsAny<IReadOnlyCollection<int>>(), 4,
            It.IsAny<CancellationToken>()), Times.Once);
        VerifySaved(Times.Once());
    }

    //D177: მოსწავლის გაყვანა მის დასწრებიან გაკვეთილს ზედმეტს გახდიდა
    [Fact]
    public async Task Create_RemovalMakingAnAttendedLessonExtra_IsRefused()
    {
        var lesson = new Lesson
        {
            Id = 1,
            GroupId = 3,
            LessonDt = new DateTime(2026, 12, 7, 15, 0, 0, DateTimeKind.Unspecified),
            TeacherContractId = 5,
            SalarySchemaId = 8,
            FourWeekHours = 8,
            TeoMinDate = new DateTime(2026, 12, 7, 15, 0, 0, DateTimeKind.Unspecified),
            TeoMaxDate = new DateTime(2026, 12, 7, 15, 0, 0, DateTimeKind.Unspecified)
        };
        lesson.LessonsByStudents.Add(new LessonByStudent
        {
            Id = 10, LessonId = 1, StudentContractId = 20, GroupByStudentId = 330, HoursCount = 2, Present = true
        });
        _groups[3].Lessons.Add(lesson);

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.RemoveStudents, 3, Day(12, 1), Removal(20)));

        Assert.Equal(nameof(GroupErrors.ChangeCreatesLessonsWithErrors), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D214: ერთდროული შენახვისას მეორე უარყოფილია
    [Fact]
    public async Task Create_ConcurrentSave_IsConflict()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("LastNumber changed"));

        Result<int> result = await Create(StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21)));

        Assert.Equal(nameof(GroupOrderErrors.OrderSavedConcurrently), result.Error.Code);
    }

    [Fact]
    public async Task UpdateAndDelete_UnknownOrder_AreNotFound()
    {
        Result updated = await Update(99, StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1), Student(21)));
        Result deleted = await Delete(99);

        Assert.Equal(nameof(GroupOrderErrors.GroupOrderNotFound), updated.Error.Code);
        Assert.Equal(nameof(GroupOrderErrors.GroupOrderNotFound), deleted.Error.Code);
        VerifySaved(Times.Never());
    }

    //ბრძანების ტიპი არ იცვლება
    [Fact]
    public async Task Update_AnotherType_IsInvalid()
    {
        SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(12, 1), 3, StudentLine(71, 21)));

        Result result = await Update(7, StudentsRequest(EGroupOrderType.ChangeStudentFees, 3, Day(12, 1),
            Student(21, id: 71)));

        Assert.Equal(nameof(GroupOrderErrors.OrderTypeIsInvalid), result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_AnotherOrdersLine_IsLineNotFound()
    {
        SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(12, 1), 3, StudentLine(71, 21)));

        Result result = await Update(7, StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1),
            Student(21, id: 33)));

        Assert.Equal(nameof(GroupOrderErrors.LineNotFound), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //ბრძანება ადგილზე იცვლება: ხაზი და მისი სტრიქონი Id-ს ინარჩუნებს, ნომერი და გამოცემა არ იცვლება
    [Fact]
    public async Task Update_StudentsOrder_RewritesItsRowAndKeepsTheNumber()
    {
        GroupOrder order = SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(12, 1), 3,
            StudentLine(71, 21)));
        _groups[3].GroupsByStudents.Add(new GroupByStudent
        {
            GbsId = 710,
            GroupId = 3,
            StudentContractId = 21,
            FourWeekHours = 8,
            FourWeekFee = 48,
            OneHourFee = 6,
            HoursCoefficient = 1,
            StartDate = Day(12, 1),
            GroupOrderStudentId = 71
        });

        Result result = await Update(7, StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 7),
            Student(21, 7m, 71)));

        Assert.True(result.IsSuccess);
        GroupByStudent row = Assert.Single(_groups[3].GroupsByStudents, s => s.StudentContractId == 21);
        Assert.Equal((710, Day(12, 7), 7m), (row.GbsId, row.StartDate, row.OneHourFee));
        Assert.Equal((Day(12, 7), 26, 7), (order.OrderDate, order.NumberYear!.Value, order.Number!.Value));
        Assert.Equal("manager", order.ChangedByUserName);
        Assert.Equal(Now.DateTime, order.ChangedAt);
        _repository.Verify(r => r.NextNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySaved(Times.Once());
    }

    //D184: წარსული თარიღის ბრძანება Manager-ს ვერ შეუცვლია
    [Fact]
    public async Task Update_PastOrderWithoutRight_IsRefused()
    {
        SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(11, 20), 3, StudentLine(71, 21)));

        Result result = await Update(7, StudentsRequest(EGroupOrderType.AddStudents, 3, Day(12, 1),
            Student(21, id: 71)));

        Assert.Equal(nameof(GroupOrderErrors.PastOrderNeedsRight), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //საწყისი ბრძანების მოთხოვნა: ჯგუფ 3-ის ველები და ხაზები, მოსწავლის საათის ფასით oneHourFee
    private static GroupOrderRequest InitialRequest(decimal oneHourFee)
    {
        return new GroupOrderRequest
        {
            OrderType = (int)EGroupOrderType.Initial,
            OrderDate = Day(9, 1),
            AcademicYearId = 11,
            GroupCode = "601",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            Teachers =
            [
                new GroupOrderTeacherRequest { Id = 31, TeacherContractId = 5, SalarySchemaId = 8, StartDate = Day(9, 1) }
            ],
            Students =
            [
                new GroupOrderStudentRequest
                {
                    Id = 33,
                    StudentContractId = 20,
                    FourWeekHours = 8,
                    FourWeekFee = 8 * oneHourFee,
                    OneHourFee = oneHourFee,
                    HoursCoefficient = 1,
                    StartDate = Day(9, 1)
                }
            ],
            DayTimePlaces =
            [
                new GroupOrderDayTimePlaceRequest
                {
                    Id = 32, WeekDayId = 1, LessonStartTimeId = 4, HoursCount = 2, RoomId = 1, StartDate = Day(9, 1)
                }
            ]
        };
    }

    //D196: საწყისი ბრძანება მხოლოდ უფლებით იცვლება
    [Fact]
    public async Task Update_InitialOrderWithoutRight_IsRefused()
    {
        Result result = await Update(3, InitialRequest(7m));

        Assert.Equal(nameof(GroupOrderErrors.InitialOrderNeedsRight), result.Error.Code);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Update_InitialOrderWithRight_RewritesTheGroupWithoutANumber()
    {
        WithPastOrdersRight();

        Result result = await Update(3, InitialRequest(7m));

        Assert.True(result.IsSuccess);
        GroupByStudent row = Assert.Single(_groups[3].GroupsByStudents);
        Assert.Equal((330, 7m, 56m), (row.GbsId, row.OneHourFee, row.FourWeekFee));
        Assert.Null(_entities[3].Number);
        VerifySaved(Times.Once());
    }

    //დაპაუზებული შექმნის ბრძანება 8 ("605"), მასწავლებლის გარეშე, თარიღით orderDate
    private GroupOrder SeedPausedOrder(DateTime orderDate)
    {
        return SeedOrder(new GroupOrderData
        {
            Id = 8,
            OrderType = EGroupOrderType.CreateGroup,
            OrderDate = orderDate,
            IsPaused = true,
            Fields = new GroupFields(11, "605", 6, 2, 10),
            Students = [StudentLine(81, 21)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(82, 1, 4, 2f, 1)]
        });
    }

    private static GroupOrderRequest PausedOrderRequest(DateTime orderDate, List<GroupOrderTeacherRequest> teachers)
    {
        return CreateGroupRequest(orderDate, teachers, [Student(21, id: 81)], [Monday(82)]);
    }

    //D186: მასწავლებლის დამატებით დაპაუზებული ბრძანება გააქტიურდება და ჯგუფს ქმნის; ნომერი უკვე აქვს
    [Fact]
    public async Task Update_ActivatingAPausedOrder_CreatesTheGroup()
    {
        GroupOrder order = SeedPausedOrder(Day(11, 30));

        Result result = await Update(8, PausedOrderRequest(Day(11, 30), [Teacher()]));

        Assert.True(result.IsSuccess);
        Group group = Assert.Single(_addedGroups);
        Assert.Same(group, order.Group);
        Assert.False(order.IsPaused);
        Assert.Equal(8, order.Number);
        Assert.Equal(81, Assert.Single(group.GroupsByStudents).GroupOrderStudentId);
        _repository.Verify(r => r.GroupCodeIsTaken(11, "605", null, 8, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.NextNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifySaved(Times.Once());
    }

    //D184, D210: გააქტიურება წარსული თარიღით მხოლოდ უფლებით
    [Fact]
    public async Task Update_ActivatingAPausedOrderInThePast_IsRefused()
    {
        SeedPausedOrder(Day(11, 20));

        Result result = await Update(8, PausedOrderRequest(Day(11, 20), [Teacher()]));

        Assert.Equal(nameof(GroupOrderErrors.PausedOrderDateIsInPast), result.Error.Code);
        Assert.Empty(_addedGroups);
        VerifySaved(Times.Never());
    }

    //D213: გააქტიურებული შექმნის ბრძანება მასწავლებლის გარეშე ისევ დაპაუზდება, ჯგუფი კი იშლება, ბრძანება რჩება
    [Fact]
    public async Task Update_RemovingTheTeacherOfAnActiveCreateOrder_PausesItAndDeletesTheGroup()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 30)));
        GroupOrder order = _entities[6];
        Group group = _groups[6];

        Result result = await Update(6,
            CreateGroupRequest(Day(11, 30), [], [Student(30, id: 63)], [Monday(62)], "606"));

        Assert.True(result.IsSuccess);
        Assert.True(order.IsPaused);
        Assert.Null(order.GroupId);
        _repository.Verify(r => r.RemoveGroup(group, 6, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.RemoveLines(It.Is<GroupOrderRemovedLines>(l => l.Teachers.Single().Id == 61)),
            Times.Once);
        Assert.Contains(30, _markedContracts);
        VerifySaved(Times.Once());
    }

    //D213, D191: ჯგუფი, რომელსაც გადაყვანა სხვა ჯგუფთან აკავშირებს, ან რომლის გაკვეთილები ხელფასშია, არ იშლება
    [Fact]
    public async Task Update_RepausingALinkedOrPaidGroup_IsRefused()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 30)));
        SeedOrder(StudentsOrder(9, EGroupOrderType.TransferStudents, Day(12, 1), 3, StudentLine(91, 20)) with
        {
            TargetGroupId = 6
        });
        GroupOrderRequest request = CreateGroupRequest(Day(11, 30), [], [Student(30, id: 63)], [Monday(62)], "606");

        Result linked = await Update(6, request);
        _orders.RemoveAll(o => o.Id == 9);
        _repository.Setup(r => r.HasSalary(6, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Result paid = await Update(6, request);

        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasTransfers), linked.Error.Code);
        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasSalary), paid.Error.Code);
        _repository.Verify(r => r.RemoveGroup(It.IsAny<Group>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Never());
    }

    //ჯგუფის წელი შექმნის ბრძანებაში იცვლება: ჯგუფის სხვა ბრძანებებიც ახალი წლისაა
    [Fact]
    public async Task Update_CreateOrderWithAnotherYear_MovesTheGroupsOrdersToIt()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 30)));

        Result result = await Update(6,
            CreateGroupRequest(Day(11, 30), [Teacher(id: 61)], [Student(30, id: 63)], [Monday(62)], "606", 12));

        Assert.True(result.IsSuccess);
        Assert.Equal(12, _groups[6].AcademicYearId);
        Assert.Equal(12, _entities[6].AcademicYearId);
        _repository.Verify(r => r.SetOrdersAcademicYear(6, 12, It.IsAny<CancellationToken>()), Times.Once);
        VerifySaved(Times.Once());
    }

    //D196: საწყისი ბრძანება არ იშლება
    [Fact]
    public async Task Delete_InitialOrder_IsRefused()
    {
        WithPastOrdersRight();

        Result result = await Delete(3);

        Assert.Equal(nameof(GroupOrderErrors.InitialOrderCannotBeDeleted), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D211: დაპაუზებული ბრძანება წარსული თარიღითაც იშლება უფლების გარეშე; ჯგუფს არ ეხება
    [Fact]
    public async Task Delete_PausedOrderInThePast_IsDeletedWithoutTheRight()
    {
        GroupOrder order = SeedPausedOrder(Day(11, 2));

        Result result = await Delete(8);

        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.RemoveOrder(order), Times.Once);
        _repository.Verify(r => r.GetOrders(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        VerifySaved(Times.Once());
    }

    //D191: შექმნის ბრძანების წაშლა ჯგუფს შლის ბრძანებებითურთ
    [Fact]
    public async Task Delete_CreateOrder_DeletesItsGroup()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 30)));
        Group group = _groups[6];

        Result result = await Delete(6);

        Assert.True(result.IsSuccess);
        _repository.Verify(r => r.RemoveGroup(group, null, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.RemoveOrder(It.IsAny<GroupOrder>()), Times.Never);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Delete_CreateOrderOfALinkedOrPaidGroup_IsRefused()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 30)));
        SeedOrder(StudentsOrder(9, EGroupOrderType.TransferStudents, Day(12, 1), 6, StudentLine(91, 30)) with
        {
            TargetGroupId = 3
        });

        Result linked = await Delete(6);
        _orders.RemoveAll(o => o.Id == 9);
        _repository.Setup(r => r.HasSalary(6, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Result paid = await Delete(6);

        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasTransfers), linked.Error.Code);
        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasSalary), paid.Error.Code);
        VerifySaved(Times.Never());
    }

    //D191: წარსული თარიღის შექმნის ბრძანების წაშლას უფლება სჭირდება
    [Fact]
    public async Task Delete_PastCreateOrderWithoutRight_IsRefused()
    {
        SeedGroup(CreateGroupOrder(6, "606", Day(11, 2)));

        Result result = await Delete(6);

        Assert.Equal(nameof(GroupOrderErrors.PastOrderNeedsRight), result.Error.Code);
        VerifySaved(Times.Never());
    }

    //D191, D198: ბრძანება იშლება გადათამაშებით: მის მიერ შექმნილი სტრიქონი იშლება
    [Fact]
    public async Task Delete_StudentsOrder_RemovesItsRowAndTheOrder()
    {
        GroupOrder order = SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(12, 1), 3,
            StudentLine(71, 21)));
        var row = new GroupByStudent
        {
            GbsId = 710, GroupId = 3, StudentContractId = 21, StartDate = Day(12, 1), GroupOrderStudentId = 71
        };
        _groups[3].GroupsByStudents.Add(row);

        Result result = await Delete(7);

        Assert.True(result.IsSuccess);
        Assert.Same(row, Assert.Single(Assert.Single(_removedRows).Students));
        _repository.Verify(r => r.RemoveOrder(order), Times.Once);
        Assert.True(_groups[3].DirtyLessons);
        VerifySaved(Times.Once());
    }

    //D185: წაშლის შემდეგ გვიანდელი ბრძანება აღარ ესადაგება: შეცდომა მის ნომერს ასახელებს
    [Fact]
    public async Task Delete_OrderALaterOrderNeeds_NamesTheLaterOrder()
    {
        SeedOrder(StudentsOrder(7, EGroupOrderType.AddStudents, Day(12, 1), 3, StudentLine(71, 21)));
        SeedOrder(StudentsOrder(9, EGroupOrderType.RemoveStudents, Day(12, 10), 3, RemovalLine(91, 21)));

        Result result = await Delete(7);

        Assert.Equal(nameof(GroupOrderErrors.OrderNoLongerFits), result.Error.Code);
        Assert.Contains("№ 26.9", result.Error.Description, StringComparison.Ordinal);
        _repository.Verify(r => r.RemoveOrder(It.IsAny<GroupOrder>()), Times.Never);
        VerifySaved(Times.Never());
    }
}
