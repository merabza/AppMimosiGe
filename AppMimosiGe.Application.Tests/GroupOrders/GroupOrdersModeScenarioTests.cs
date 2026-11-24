using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.GetGroupOrdersExportFile;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupEditing;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Infrastructure.GroupOrders;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Moq;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrderRequests;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersInMemoryData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდები ნამდვილ handler-ებზე, ვალიდატორზე, რეპოზიტორიებზე და ფაილების საქაღალდეზე, EF InMemory ბაზაზე (D194,
//D195): გადასვლა ბრძანებებზე, ნომრიანი ბრძანება, დაბრუნება ჯგუფების რედაქტირებაზე და ხელახალი გადასვლა. ჯგუფების
//სტრიქონები და გაკვეთილები უცვლელი რჩება; ნომრის მთვლელიც რჩება, ამიტომ ნომერი თავიდან არ გაიცემა (D192). ყოველი
//ნაბიჯი ახალი კონტექსტითაა, როგორც ცალკე მოთხოვნა. დღეს 24.11.2026 16:30
public sealed class GroupOrdersModeScenarioTests : IDisposable
{
    private readonly Mock<IUserClaimRights> _claimRights = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly string _exportFolder =
        Path.Combine(Path.GetTempPath(), $"GroupOrdersModeScenarioTests_{Guid.NewGuid():N}");

    private readonly DbContextOptions<MimosiGeDbContext> _options = NewDatabase();
    private readonly GroupOrdersSettings _settings;
    private static readonly DateTime Now = new(2026, 11, 24, 16, 30, 0, DateTimeKind.Unspecified);
    private readonly TimeProvider _timeProvider = TimeProvider(Now);

    //ჯგუფი 1 ("605") 01.09-იდან მოქმედია, ორი გაკვეთილით; ჯგუფ 2-ს ("606") მასწავლებელი 10.11-ს დაუსრულდა და საწყისს
    //ვერ მიიღებს (D196)
    public GroupOrdersModeScenarioTests()
    {
        _currentUser.Setup(u => u.Name).Returns("admin");
        _settings = new GroupOrdersSettings { ExportFolder = _exportFolder };
        Group active = StoredGroup(1, "605");
        Group inactive = StoredGroup(2, "606", teacherContractId: 6);
        inactive.GroupsByTeachers.Single().EndDate = Day(11, 10);
        active.Lessons.Add(Lesson(1, Day(9, 7), (21, 12, true), (41, 13, false)));
        active.Lessons.Add(Lesson(2, Day(11, 16), (21, 12, true)));
        using MimosiGeDbContext context = NewContext();
        Seed(context, active, inactive);
    }

    public void Dispose()
    {
        if (Directory.Exists(_exportFolder))
        {
            Directory.Delete(_exportFolder, true);
        }
    }

    private static Lesson Lesson(int id, DateTime day,
        params (int StudentContractId, int GbsId, bool Present)[] students)
    {
        DateTime lessonDt = day.AddHours(15);
        var lesson = new Lesson
        {
            Id = id,
            GroupId = 1,
            TeacherContractId = 5,
            SalarySchemaId = 8,
            LessonDt = lessonDt,
            FourWeekHours = 8f,
            TeoMinDate = lessonDt,
            TeoMaxDate = lessonDt
        };
        foreach ((int studentContractId, int gbsId, bool present) in students)
        {
            lesson.LessonsByStudents.Add(new LessonByStudent
            {
                Id = id * 10 + gbsId,
                StudentContractId = studentContractId,
                GroupByStudentId = gbsId,
                HoursCount = 2f,
                Present = present
            });
        }

        return lesson;
    }

    private MimosiGeDbContext NewContext()
    {
        return new MimosiGeDbContext(_options, true);
    }

    private async Task<SwitchToGroupOrdersResponse> SwitchToOrders(bool dryRun)
    {
        await using MimosiGeDbContext context = NewContext();
        var groupsRepository = new GroupsRepository(context);
        Result<SwitchToGroupOrdersResponse> result = await new SwitchToGroupOrdersCommandHandler(
                new GroupOrdersRepository(context), groupsRepository,
                new GroupRequestValidator(groupsRepository, new StudentContractsRepository(context),
                    new TeacherContractsRepository(context)), UnitOfWork(context), _currentUser.Object, _timeProvider)
            .Handle(new SwitchToGroupOrdersCommand(dryRun), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private GroupOrdersExportFiles ExportFiles()
    {
        return new GroupOrdersExportFiles(_settings, NullLogger<GroupOrdersExportFiles>.Instance);
    }

    private async Task<SwitchToGroupEditingResponse> SwitchToEditing()
    {
        await using MimosiGeDbContext context = NewContext();
        Result<SwitchToGroupEditingResponse> result = await new SwitchToGroupEditingCommandHandler(
                new GroupOrdersRepository(context), ExportFiles(), _settings, UnitOfWork(context),
                _currentUser.Object, _timeProvider)
            .Handle(new SwitchToGroupEditingCommand(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    //ბრძანება ისე, როგორც API-დან (C2): ჯერ ვალიდატორი, მერე handler-ი
    private async Task<int> CreateOrder(GroupOrderRequest request)
    {
        await using MimosiGeDbContext context = NewContext();
        var repository = new GroupOrdersRepository(context);
        var groupsRepository = new GroupsRepository(context);
        var studentContractsRepository = new StudentContractsRepository(context);
        var teacherContractsRepository = new TeacherContractsRepository(context);
        var command = new CreateGroupOrderCommand(request);
        ValidationResult validation = await new CreateGroupOrderCommandValidator(repository, groupsRepository,
            studentContractsRepository, teacherContractsRepository).ValidateAsync(command);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors.Select(e => e.ErrorCode)));
        Result<int> result = await new CreateGroupOrderCommandHandler(repository, groupsRepository,
            studentContractsRepository,
            new GroupRequestValidator(groupsRepository, studentContractsRepository, teacherContractsRepository),
            new LessonGeneratorRepository(context), UnitOfWork(context), _claimRights.Object, _currentUser.Object,
            _timeProvider, _settings).Handle(command, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    //დაპაუზებული შექმნის ბრძანება (D186): ნომერს იღებს, ჯგუფებს კი არ ცვლის
    private Task<int> CreatePausedOrder(string groupCode)
    {
        return CreateOrder(CreateGroupRequest(Day(12, 1), [], [Student(22)], [], groupCode));
    }

    //ჯგუფები, სტრიქონები ხაზის კავშირის გარეშე, გაკვეთილები, დასწრება და კონტრაქტების ალმები, როგორც ბაზაშია
    private object Snapshot()
    {
        using MimosiGeDbContext context = NewContext();
        return new
        {
            Groups = context.Groups.OrderBy(g => g.GrpId).Select(g => new
            {
                g.GrpId, g.AcademicYearId, g.GroupCode, g.CourseId, g.GroupSizeId, g.StudentStatusId, g.VoidDate,
                g.DirtyLessons
            }).ToList(),
            Teachers = context.GroupsByTeachers.OrderBy(t => t.Id)
                .Select(t => new { t.Id, t.GroupId, t.TeacherContractId, t.SalarySchemaId, t.StartDate, t.EndDate })
                .ToList(),
            Students = context.GroupsByStudents.OrderBy(s => s.GbsId).Select(s => new
            {
                s.GbsId, s.GroupId, s.StudentContractId, s.FourWeekHours, s.FourWeekFee, s.OneHourFee,
                s.HoursCoefficient, s.Note, s.StartDate, s.EndDate
            }).ToList(),
            DayTimePlaces = context.GroupDayTimePlaces.OrderBy(d => d.GdtpId).Select(d => new
            {
                d.GdtpId, d.GroupId, d.WeekDayId, d.LessonStartTimeId, d.HoursCount, d.RoomId, d.StartDate,
                d.EndDate
            }).ToList(),
            Lessons = context.Lessons.OrderBy(l => l.Id).Select(l => new
            {
                l.Id, l.GroupId, l.LessonDt, l.TeacherContractId, l.SalarySchemaId, l.FourWeekHours,
                l.LessonStatusId, l.TeoMinDate, l.TeoMaxDate
            }).ToList(),
            LessonStudents = context.LessonsByStudents.OrderBy(s => s.Id).Select(s => new
            {
                s.Id, s.LessonId, s.StudentContractId, s.GroupByStudentId, s.HoursCount, s.Present
            }).ToList(),
            Contracts = context.StudentContracts.OrderBy(s => s.ScId).Select(s => new { s.ScId, s.DirtyNextPayDate })
                .ToList()
        };
    }

    private List<(int Id, EGroupOrderType OrderType, int? GroupId, int? Number)> StoredOrders()
    {
        using MimosiGeDbContext context = NewContext();
        return
        [
            .. context.GroupOrders.OrderBy(o => o.Id).ToList()
                .Select(o => (o.Id, o.OrderType, o.GroupId, o.Number))
        ];
    }

    private List<(int NumberYear, int LastNumber)> StoredCounters()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.GroupOrderNumbers.ToList().Select(n => (n.NumberYear, n.LastNumber))];
    }

    private async Task<GroupOrdersModeResponse> StoredMode()
    {
        await using MimosiGeDbContext context = NewContext();
        return await new GroupOrdersRepository(context).GetMode();
    }

    //ჯგუფის 4 სტრიქონიდან linked ხაზთან დაკავშირებულია, ხაზები კი orderIds ბრძანებებისაა
    private void AssertLinks(int groupId, int linked, params int[] orderIds)
    {
        using MimosiGeDbContext context = NewContext();
        List<int?> lines =
        [
            .. context.GroupsByTeachers.Where(r => r.GroupId == groupId).Select(r => r.GroupOrderTeacherId),
            .. context.GroupsByStudents.Where(r => r.GroupId == groupId).Select(r => r.GroupOrderStudentId),
            .. context.GroupDayTimePlaces.Where(r => r.GroupId == groupId).Select(r => r.GroupOrderDayTimePlaceId)
        ];
        List<int> lineOrderIds =
        [
            .. context.GroupOrderTeachers.Where(l => lines.Contains(l.Id)).Select(l => l.GroupOrderId),
            .. context.GroupOrderStudents.Where(l => lines.Contains(l.Id)).Select(l => l.GroupOrderId),
            .. context.GroupOrderDayTimePlaces.Where(l => lines.Contains(l.Id)).Select(l => l.GroupOrderId)
        ];
        Assert.Equal((4, linked), (lines.Count, lines.Count(l => l is not null)));
        Assert.Equal(orderIds, lineOrderIds.Distinct());
    }

    [Fact]
    public async Task SwitchToOrdersAndBack_KeepsRowsLessonsAndTheNumberCounter()
    {
        object before = Snapshot();

        //შემოწმება: ჯგუფი 1 საწყისს 01.09-ით მიიღებს, ჯგუფი 2 ვერა; არაფერი იცვლება
        SwitchToGroupOrdersResponse check = await SwitchToOrders(true);
        Assert.Equal(
        [
            new GroupOrdersSwitchGroupResponse(1, "605", Day(9, 1)),
            new GroupOrdersSwitchGroupResponse(2, "606", null)
        ], check.Groups);
        Assert.Empty(check.Violations);
        Assert.Empty(StoredOrders());

        //გადასვლა: საწყისი ბრძანება ჯგუფ 1-ს, მისი ყოველი სტრიქონი თავის ხაზს უკავშირდება; ჯგუფი 2 უცვლელია
        SwitchToGroupOrdersResponse switched = await SwitchToOrders(false);
        Assert.True(switched.Switched);
        (int initialId, _, _, _) = Assert.Single(StoredOrders());
        Assert.Equal([(initialId, EGroupOrderType.Initial, (int?)1, (int?)null)], StoredOrders());
        AssertLinks(1, 4, initialId);
        AssertLinks(2, 0);
        Assert.Equal(new GroupOrdersModeResponse(true, Day(11, 24), "admin", Now), await StoredMode());
        Assert.Equivalent(before, Snapshot(), true);

        //ნომრიანი ბრძანება: დაპაუზებული შექმნის ბრძანება 26.1, ჯგუფებს არ ცვლის
        int paused = await CreatePausedOrder("607");
        Assert.Equal([(26, 1)], StoredCounters());

        //დაბრუნება: ჯერ ფაილი ორივე ბრძანებით, მერე ბრძანებები და კავშირები იშლება; მთვლელი რჩება
        SwitchToGroupEditingResponse editing = await SwitchToEditing();
        Assert.Equal(new SwitchToGroupEditingResponse("GroupOrders_20261124163000.json", 2), editing);
        Assert.Empty(StoredOrders());
        AssertLinks(1, 0);
        Assert.Equal([(26, 1)], StoredCounters());
        Assert.Equal(new GroupOrdersModeResponse(false, null, "admin", Now), await StoredMode());
        Assert.Equivalent(before, Snapshot(), true);

        byte[] file = await File.ReadAllBytesAsync(Path.Combine(_exportFolder, editing.FileName));
        using (var document = JsonDocument.Parse(file))
        {
            Assert.Equal([(initialId, 0), (paused, 1)],
                document.RootElement.GetProperty("Orders").EnumerateArray()
                    .Select(o => (o.GetProperty("Id").GetInt32(), o.GetProperty("OrderType").GetInt32())));
        }

        //ფაილის გადმოწერა
        Result<GroupOrdersExportFile> download = await new GetGroupOrdersExportFileQueryHandler(ExportFiles(),
            _settings).Handle(new GetGroupOrdersExportFileQuery(editing.FileName), CancellationToken.None);
        Assert.Equal(file, download.Value.Content);

        //ხელახალი გადასვლა: მხოლოდ საწყისი ბრძანებები; ახალი ნომერი მთვლელიდან გრძელდება
        Assert.True((await SwitchToOrders(false)).Switched);
        AssertLinks(1, 4, StoredOrders().Single(o => o.OrderType == EGroupOrderType.Initial).Id);
        int pausedAgain = await CreatePausedOrder("608");
        Assert.Equal([EGroupOrderType.Initial, EGroupOrderType.CreateGroup],
            StoredOrders().Select(o => o.OrderType));
        Assert.Equal((int?)2, StoredOrders().Single(o => o.Id == pausedAgain).Number);
        Assert.Equal([(26, 2)], StoredCounters());
        Assert.Equivalent(before, Snapshot(), true);
    }
}
