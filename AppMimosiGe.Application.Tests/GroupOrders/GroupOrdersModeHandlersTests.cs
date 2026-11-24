using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.Tests.Groups;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდების handler-ები mock რეპოზიტორიებით (D194, D195, D219, D221). დღეს 24.11.2026 16:30, ანუ გადასვლის დღე
//(D0) 24.11.2026. დამგეგმავი, შემოწმებები და ჩაწერა ნამდვილია
public sealed class GroupOrdersModeHandlersTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 16, 30, 0, DateTimeKind.Unspecified);
    private readonly List<GroupOrder> _addedOrders = [];
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IGroupOrdersExportFiles> _exportFiles = new();
    private readonly List<Group> _groups = [];
    private readonly Mock<IGroupsRepository> _groupsRepository = GroupTestData.RepositoryWhereEverythingExists();
    private readonly List<GroupOrder> _orders = [];
    private readonly List<GroupOrder> _removedOrders = [];
    private readonly Mock<IGroupOrdersRepository> _repository = new();
    private readonly GroupOrdersSettings _settings = new() { ExportFolder = "D:\\GroupOrdersExport" };
    private readonly List<string> _steps = [];
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private GroupOrdersMode? _addedMode;
    private GroupOrderLinkedRows _linkedRows = new([], [], []);
    private GroupOrdersMode? _mode;

    public GroupOrdersModeHandlersTests()
    {
        _currentUser.Setup(u => u.Name).Returns("admin");
        _repository.Setup(r => r.GetModeForChange(It.IsAny<CancellationToken>())).ReturnsAsync(() => _mode);
        _repository.Setup(r => r.GetAllGroupsForChange(It.IsAny<CancellationToken>())).ReturnsAsync(() => _groups);
        _repository.Setup(r => r.AddOrder(It.IsAny<GroupOrder>())).Callback((GroupOrder o) => _addedOrders.Add(o));
        _repository.Setup(r => r.AddMode(It.IsAny<GroupOrdersMode>()))
            .Callback((GroupOrdersMode m) => _addedMode = m);
        _repository.Setup(r => r.GetAllOrdersForChange(It.IsAny<CancellationToken>())).ReturnsAsync(() => _orders);
        _repository.Setup(r => r.GetLinkedRowsForChange(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _linkedRows);
        _repository.Setup(r => r.RemoveOrder(It.IsAny<GroupOrder>())).Callback((GroupOrder o) =>
        {
            _steps.Add("remove");
            _removedOrders.Add(o);
        });
        _exportFiles.Setup(f => f.TryWrite(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => _steps.Add("write")).ReturnsAsync(true);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => _steps.Add("save"))
            .Returns(Task.CompletedTask);

        _groupsRepository.Setup(r =>
                r.GetStudentContractNames(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<int> ids, CancellationToken _) =>
                ids.ToDictionary(id => id, id => $"მოსწავლე {id}"));
        _groupsRepository.Setup(r => r.GetTeacherContracts(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new GroupTeacherContractLookupResponse(5, "მასწავლებელი 5", 8),
            new GroupTeacherContractLookupResponse(6, "მასწავლებელი 6", 8)
        ]);
        _groupsRepository.Setup(r => r.GetWeekDays(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new LookupItemResponse(1, "ორშაბათი")]);
        _groupsRepository.Setup(r => r.GetLessonStartTimesById(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, TimeOnly> { [4] = new(15, 0) });
    }

    private static TimeProvider TimeProvider()
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Now, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        return timeProvider.Object;
    }

    private Task<Result<SwitchToGroupOrdersResponse>> SwitchToOrders(bool dryRun)
    {
        var validator = new GroupRequestValidator(_groupsRepository.Object,
            GroupTestData.StudentContractsRepositoryWhereEverythingExists().Object,
            GroupTestData.TeacherContractsRepositoryWhereEverythingExists().Object);
        return new SwitchToGroupOrdersCommandHandler(_repository.Object, _groupsRepository.Object, validator,
                _unitOfWork.Object, _currentUser.Object, TimeProvider())
            .Handle(new SwitchToGroupOrdersCommand(dryRun), CancellationToken.None);
    }

    private Task<Result<SwitchToGroupEditingResponse>> SwitchToEditing()
    {
        return new SwitchToGroupEditingCommandHandler(_repository.Object, _exportFiles.Object, _settings,
                _unitOfWork.Object, _currentUser.Object, TimeProvider())
            .Handle(new SwitchToGroupEditingCommand(), CancellationToken.None);
    }

    //ჯგუფი 4 ("605") მოქმედია 01.09-იდან, ჯგუფი 5 ("606") 10.11-იდან მასწავლებლის გარეშეა და საწყისს ვერ მიიღებს
    private (Group Active, Group Inactive) SeedGroups()
    {
        Group active = StoredGroup(4, "605");
        Group inactive = StoredGroup(5, "606", teacherContractId: 6);
        inactive.GroupsByTeachers.Single().EndDate = Day(11, 10);
        _groups.AddRange([active, inactive]);
        return (active, inactive);
    }

    private void VerifyNothingChanged()
    {
        Assert.Empty(_addedOrders);
        Assert.Null(_addedMode);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- ვიზარდი 1

    [Fact]
    public async Task SwitchToOrders_InOrdersMode_IsRefused()
    {
        _mode = new GroupOrdersMode { Id = 1, IsOrdersMode = true };

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(true);

        Assert.Equal(GroupOrderErrors.GroupOrdersAreAlreadyEnabled, result.Error);
        _repository.Verify(r => r.GetAllGroupsForChange(It.IsAny<CancellationToken>()), Times.Never);
    }

    //D194, D196: რეპორტი ყოველ ჯგუფს საწყისი ბრძანების თარიღით ან მის გარეშე ჩამოთვლის; dry-run არაფერს ცვლის
    [Fact]
    public async Task SwitchToOrders_DryRun_ReportsTheGroupsAndChangesNothing()
    {
        (Group active, _) = SeedGroups();

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(true);

        SwitchToGroupOrdersResponse response = result.Value;
        Assert.Equal((true, false, Day(11, 24)), (response.DryRun, response.Switched, response.TransitionDate));
        Assert.Empty(response.Violations);
        Assert.Equal(
        [
            new GroupOrdersSwitchGroupResponse(4, "605", Day(9, 1)),
            new GroupOrdersSwitchGroupResponse(5, "606", null)
        ], response.Groups);
        VerifyNothingChanged();
        Assert.All(active.GroupsByStudents, s => Assert.Null(s.GroupOrderStudent));
    }

    //D194, D212, D219: ნამდვილი გაშვებაც ამოწმებს; დარღვევებისას რეპორტს აბრუნებს და არაფერს ცვლის. ჯგუფი 6 ("607")
    //იმავე მასწავლებლითა და დროით 4-ს ეფარება; ჯგუფ 4-ში მოსწავლის საათები დღიდან ფორმულას არ ესადაგება
    [Fact]
    public async Task SwitchToOrders_Violations_AreReportedAndNothingChanges()
    {
        (Group active, _) = SeedGroups();
        active.GroupsByStudents.First().FourWeekHours = 6f;
        active.GroupsByStudents.First().FourWeekFee = 36m;
        _groups.Add(StoredGroup(6, "607"));

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(false);

        SwitchToGroupOrdersResponse response = result.Value;
        Assert.Equal((false, false), (response.DryRun, response.Switched));
        Assert.Equal(
        [
            (4, nameof(GroupErrors.TeacherTimesOverlap), (DateTime?)Day(9, 1), "მასწავლებელი 5"),
            (4, nameof(GroupOrderErrors.StudentHoursDoNotMatchSchedule), Day(11, 24), "მოსწავლე 24"),
            (6, nameof(GroupErrors.TeacherTimesOverlap), Day(9, 1), "მასწავლებელი 5")
        ], response.Violations.Select(v => (v.GrpId, v.ErrorCode, v.Date, v.Row)));
        Assert.Contains("მასწავლებელი 5 — ჯგუფი 607, ორშაბათი 15:00–17:00, 01.09.2026-დან",
            response.Violations[0].ErrorText, StringComparison.Ordinal);
        Assert.Equal(3, response.Groups.Count);
        VerifyNothingChanged();
    }

    //D194: ჯგუფის რედაქტორის ვალიდატორი მთელ ისტორიაზე; დარღვევას სტრიქონი და მისი დაწყება აქვს
    [Fact]
    public async Task SwitchToOrders_InvalidRowOfAGroupWithInitialOrder_IsReported()
    {
        (Group active, _) = SeedGroups();
        active.GroupsByStudents.Last().Note = new string('ა', 256);

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(true);

        GroupOrdersSwitchViolationResponse violation = Assert.Single(result.Value.Violations);
        Assert.Equal((4, GroupErrors.NoteIsTooLong.Code, (DateTime?)Day(9, 1), "მოსწავლე 44"),
            (violation.GrpId, violation.ErrorCode, violation.Date, violation.Row));
    }

    //D194: ჯგუფი, რომელიც საწყისს ვერ მიიღებს, არ მოწმდება: მისი სტრიქონები ბრძანებით აღარ შეიცვლება (D202)
    [Fact]
    public async Task SwitchToOrders_GroupWithoutInitialOrder_IsNotChecked()
    {
        (_, Group inactive) = SeedGroups();
        inactive.GroupDayTimePlaces.Single().HoursCount = 0f;

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(true);

        Assert.Empty(result.Value.Violations);
    }

    //D194, D196: ერთ ტრანზაქციაში საწყისი ბრძანება მხოლოდ მიმღებ ჯგუფს, მისი სტრიქონების კავშირი ხაზებთან და
    //რეჟიმი, D0-ით
    [Fact]
    public async Task SwitchToOrders_Run_CreatesInitialOrdersLinksRowsAndEnablesTheMode()
    {
        (Group active, Group inactive) = SeedGroups();

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(false);

        Assert.Equal((false, true), (result.Value.DryRun, result.Value.Switched));
        GroupOrder order = Assert.Single(_addedOrders);
        Assert.Equal((EGroupOrderType.Initial, (int?)4, Day(9, 1), "admin", Now),
            (order.OrderType, order.GroupId, order.OrderDate, order.CreatedByUserName, order.CreatedAt));
        Assert.All(active.GroupsByStudents, s => Assert.Contains(s.GroupOrderStudent!, order.GroupOrderStudents));
        Assert.Same(order.GroupOrderTeachers.Single(), active.GroupsByTeachers.Single().GroupOrderTeacher);
        Assert.Same(order.GroupOrderDayTimePlaces.Single(),
            active.GroupDayTimePlaces.Single().GroupOrderDayTimePlace);
        Assert.All(inactive.GroupsByStudents, s => Assert.Null(s.GroupOrderStudent));
        Assert.Equal((GroupOrdersMode.SingleRowId, true, (DateTime?)Day(11, 24), "admin", (DateTime?)Now),
            (_addedMode!.Id, _addedMode.IsOrdersMode, _addedMode.TransitionDate, _addedMode.SwitchedByUserName,
                _addedMode.SwitchedAt));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    //ვიზარდი 2-ის შემდეგ რეჟიმის სტრიქონი არსებობს და იცვლება (მისი SwitchedAt concurrency token-ია, D221)
    [Fact]
    public async Task SwitchToOrders_ExistingModeRow_IsSwitched()
    {
        SeedGroups();
        _mode = new GroupOrdersMode
        {
            Id = 1, IsOrdersMode = false, SwitchedByUserName = "old", SwitchedAt = Day(11, 1)
        };

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(false);

        Assert.True(result.Value.Switched);
        Assert.Null(_addedMode);
        Assert.Equal((true, (DateTime?)Day(11, 24), "admin", (DateTime?)Now),
            (_mode.IsOrdersMode, _mode.TransitionDate, _mode.SwitchedByUserName, _mode.SwitchedAt));
    }

    //D221: ერთდროული გაშვება ან ამასობაში წაშლილი სტრიქონი
    [Fact]
    public async Task SwitchToOrders_ConcurrentSave_IsRefused()
    {
        SeedGroups();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        Result<SwitchToGroupOrdersResponse> result = await SwitchToOrders(false);

        Assert.Equal(GroupOrderErrors.ModeSwitchedConcurrently, result.Error);
    }

    // --- ვიზარდი 2

    //ბრძანებების რეჟიმი: ჯგუფ 4-ის საწყისი ბრძანება (Id 1) ხაზებით და მისი სტრიქონები კავშირებით
    private (Group Group, GroupOrder Order) SeedOrdersMode()
    {
        _mode = new GroupOrdersMode
        {
            Id = 1, IsOrdersMode = true, TransitionDate = Day(11, 20), SwitchedAt = Day(11, 20)
        };
        Group group = StoredGroup(4, "605");
        GroupOrder order = StoredInitialOrder(group, 1, 101);
        _orders.Add(order);
        _linkedRows = LinkedRows(group);
        return (group, order);
    }

    [Fact]
    public async Task SwitchToEditing_GroupsMode_IsRefused()
    {
        Result<SwitchToGroupEditingResponse> withoutRow = await SwitchToEditing();
        _mode = new GroupOrdersMode { Id = 1, IsOrdersMode = false };
        Result<SwitchToGroupEditingResponse> groupsMode = await SwitchToEditing();

        Assert.Equal(GroupOrderErrors.GroupOrdersAreAlreadyDisabled, withoutRow.Error);
        Assert.Equal(GroupOrderErrors.GroupOrdersAreAlreadyDisabled, groupsMode.Error);
        _repository.Verify(r => r.GetAllOrdersForChange(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task SwitchToEditing_WithoutExportFolder_IsRefused(string? exportFolder)
    {
        SeedOrdersMode();
        _settings.ExportFolder = exportFolder;

        Result<SwitchToGroupEditingResponse> result = await SwitchToEditing();

        Assert.Equal(GroupOrderErrors.ExportFolderIsNotConfigured, result.Error);
        Assert.Empty(_steps);
    }

    //D195: ფაილის გარეშე არაფერი იშლება
    [Fact]
    public async Task SwitchToEditing_FileNotWritten_DeletesNothing()
    {
        (Group group, _) = SeedOrdersMode();
        _exportFiles.Setup(f => f.TryWrite(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        Result<SwitchToGroupEditingResponse> result = await SwitchToEditing();

        Assert.Equal(GroupOrderErrors.ExportFileWriteFailed, result.Error);
        Assert.Empty(_removedOrders);
        Assert.All(group.GroupsByStudents, s => Assert.NotNull(s.GroupOrderStudent));
        Assert.True(_mode!.IsOrdersMode);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    //D195: ჯერ ფაილი, მერე ერთ ტრანზაქციაში კავშირები, ბრძანებები და რეჟიმი. ნომრის მთვლელს ვიზარდი არ ეხება
    [Fact]
    public async Task SwitchToEditing_WritesTheFileThenDeletesTheOrdersAndLinks()
    {
        (Group group, GroupOrder order) = SeedOrdersMode();
        string? fileName = null;
        byte[]? content = null;
        _exportFiles.Setup(f => f.TryWrite(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback((string name, byte[] bytes, CancellationToken _) =>
            {
                _steps.Add("write");
                (fileName, content) = (name, bytes);
            }).ReturnsAsync(true);

        Result<SwitchToGroupEditingResponse> result = await SwitchToEditing();

        Assert.Equal(new SwitchToGroupEditingResponse("GroupOrders_20261124163000.json", 1), result.Value);
        Assert.Equal(["write", "remove", "save"], _steps);
        Assert.Equal("GroupOrders_20261124163000.json", fileName);
        string json = Encoding.UTF8.GetString(content!);
        Assert.Contains("\"ExportedByUserName\": \"admin\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RowId\": 42", json, StringComparison.Ordinal);
        Assert.Equal([order], _removedOrders);
        Assert.All(group.GroupsByStudents, s => Assert.Null(s.GroupOrderStudent));
        Assert.Null(group.GroupsByTeachers.Single().GroupOrderTeacher);
        Assert.Null(group.GroupDayTimePlaces.Single().GroupOrderDayTimePlace);
        Assert.Equal((false, (DateTime?)null, "admin", (DateTime?)Now),
            (_mode!.IsOrdersMode, _mode.TransitionDate, _mode.SwitchedByUserName, _mode.SwitchedAt));
        _repository.Verify(r => r.NextNumber(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    //D221: ერთდროული გაშვებისას მეორე უარყოფილია; ჩაწერილი ფაილი რჩება
    [Fact]
    public async Task SwitchToEditing_ConcurrentSave_IsRefused()
    {
        SeedOrdersMode();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        Result<SwitchToGroupEditingResponse> result = await SwitchToEditing();

        Assert.Equal(GroupOrderErrors.ModeSwitchedConcurrently, result.Error);
    }

    // --- ფაილის გადმოწერა

    private Task<Result<GroupOrdersExportFile>> ExportFile(string fileName)
    {
        return new GetGroupOrdersExportFileQueryHandler(_exportFiles.Object, _settings)
            .Handle(new GetGroupOrdersExportFileQuery(fileName), CancellationToken.None);
    }

    [Fact]
    public async Task ExportFile_Existing_IsReturned()
    {
        byte[] content = [1, 2, 3];
        _exportFiles.Setup(f => f.TryRead("GroupOrders_20261124163000.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        Result<GroupOrdersExportFile> result = await ExportFile("GroupOrders_20261124163000.json");

        Assert.Equal("GroupOrders_20261124163000.json", result.Value.FileName);
        Assert.Same(content, result.Value.Content);
    }

    [Fact]
    public async Task ExportFile_Missing_IsNotFound()
    {
        _exportFiles.Setup(f => f.TryRead(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        Result<GroupOrdersExportFile> result = await ExportFile("GroupOrders_20261124163000.json");

        Assert.Equal(GroupOrderErrors.ExportFileNotFound, result.Error);
    }

    //სხვა სახელით საქაღალდის გარეთ ფაილი ვერ წაიკითხება
    [Fact]
    public async Task ExportFile_OtherName_IsNotFoundWithoutReading()
    {
        Result<GroupOrdersExportFile> result = await ExportFile("..\\appsettings.json");

        Assert.Equal(GroupOrderErrors.ExportFileNotFound, result.Error);
        _exportFiles.Verify(f => f.TryRead(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExportFile_WithoutExportFolder_IsRefused()
    {
        _settings.ExportFolder = null;

        Result<GroupOrdersExportFile> result = await ExportFile("GroupOrders_20261124163000.json");

        Assert.Equal(GroupOrderErrors.ExportFolderIsNotConfigured, result.Error);
    }
}
