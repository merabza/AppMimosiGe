using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.SwitchToGroupOrders;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Moq;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrderRequests;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersInMemoryData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.AcademicYears;

//წლის დახურვა ბრძანებების რეჟიმში (D197, D220) ნამდვილ handler-ებზე, ვალიდატორზე, რეპოზიტორიებზე და გენერატორზე, EF
//InMemory ბაზაზე. ყოველი ჯგუფი თავის DI scope-შია, როგორც პროგრამაში. ჯგუფები 1 ("605") და 2 ("606") საწყისს ვიზარდი
//1-ით იღებენ, ჯგუფი 3 ("607") ვერა: მისი მასწავლებელი 10.11-ს დასრულდა. დღეს 24.11.2026, დახურვის დღე 15.12.2026.
//მხოლოდ სინთეზური მონაცემები
public sealed class CloseAcademicYearGroupsByOrdersTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 11, 24, 16, 30, 0, DateTimeKind.Unspecified);
    private static readonly DateTime CloseDate = Day(12, 15);
    private readonly Mock<IUserClaimRights> _claimRights = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly DbContextOptions<MimosiGeDbContext> _options = NewDatabase();
    private readonly ServiceProvider _services;
    private readonly GroupOrdersSettings _settings = new();
    private readonly TimeProvider _timeProvider = TimeProvider(Now);

    //ჯგუფ 3-ის მასწავლებელიც 6-ია, ამიტომ მისი გაკვეთილი ოთხშაბათსაა: მას ჯგუფ 2-ის დრო არ ეფარება
    public CloseAcademicYearGroupsByOrdersTests()
    {
        _currentUser.Setup(u => u.Name).Returns("admin");
        Group inactive = StoredGroup(3, "607", teacherContractId: 6);
        inactive.GroupsByTeachers.Single().EndDate = Day(11, 10);
        inactive.GroupDayTimePlaces.Single().WeekDayId = 3;
        using MimosiGeDbContext context = NewContext();
        Seed(context, StoredGroup(1, "605"), StoredGroup(2, "606", teacherContractId: 6), inactive);
        _services = Services();
    }

    public void Dispose()
    {
        _services.Dispose();
    }

    private MimosiGeDbContext NewContext()
    {
        return new MimosiGeDbContext(_options, true);
    }

    //პროგრამის სერვისები, რაც ჯგუფის scope-ს სჭირდება: ერთი კონტექსტი scope-ზე
    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => NewContext());
        services.AddScoped<IMimosiGeDbContext>(sp => sp.GetRequiredService<MimosiGeDbContext>());
        services.AddScoped(sp => UnitOfWork(sp.GetRequiredService<MimosiGeDbContext>()));
        services.AddScoped<IGroupOrdersRepository, GroupOrdersRepository>();
        services.AddScoped<IGroupsRepository, GroupsRepository>();
        services.AddScoped<IStudentContractsRepository, StudentContractsRepository>();
        services.AddScoped<ITeacherContractsRepository, TeacherContractsRepository>();
        services.AddScoped<ILessonGeneratorRepository, LessonGeneratorRepository>();
        services.AddScoped<IValidator<GroupRequest>, GroupRequestValidator>();
        services.AddSingleton(_claimRights.Object);
        return services.BuildServiceProvider();
    }

    private async Task<Result<CloseAcademicYearGroupsResponse>> CloseGroups(bool dryRun)
    {
        await using MimosiGeDbContext context = NewContext();
        return await new CloseAcademicYearGroupsCommandHandler(new AcademicYearsRepository(context),
                new GroupOrdersRepository(context), new LessonGeneratorRepository(context), UnitOfWork(context),
                _services.GetRequiredService<IServiceScopeFactory>(), _currentUser.Object, _settings, _timeProvider)
            .Handle(new CloseAcademicYearGroupsCommand(11, CloseDate, dryRun), CancellationToken.None);
    }

    private async Task SwitchToOrders()
    {
        await using MimosiGeDbContext context = NewContext();
        var groupsRepository = new GroupsRepository(context);
        Result<SwitchToGroupOrdersResponse> result = await new SwitchToGroupOrdersCommandHandler(
                new GroupOrdersRepository(context), groupsRepository,
                new GroupRequestValidator(groupsRepository, new StudentContractsRepository(context),
                    new TeacherContractsRepository(context)), UnitOfWork(context), _currentUser.Object, _timeProvider)
            .Handle(new SwitchToGroupOrdersCommand(false), CancellationToken.None);
        Assert.True(result.Value.Switched);
    }

    //ბრძანება ისე, როგორც API-დან (C2): ჯერ ვალიდატორი, მერე handler-ი
    private async Task CreateOrder(GroupOrderRequest request)
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
    }

    private List<(int GrpId, DateTime? VoidDate)> StoredVoidDates()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.Groups.OrderBy(g => g.GrpId).ToList().Select(g => (g.GrpId, g.VoidDate))];
    }

    private List<GroupOrder> StoredCloseOrders()
    {
        using MimosiGeDbContext context = NewContext();
        return
        [
            .. context.GroupOrders.Where(o => o.OrderType == EGroupOrderType.CloseGroup).OrderBy(o => o.Id)
                .ToList()
        ];
    }

    private List<DateTime> StoredLessonDays(int groupId)
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.Lessons.Where(l => l.GroupId == groupId).OrderBy(l => l.LessonDt).Select(l => l.LessonDt)];
    }

    //ჯგუფების რეჟიმში ჯგუფები ბრძანების გარეშე იხურება, როგორც ბრძანებებამდე
    [Fact]
    public async Task CloseGroups_GroupsMode_ClosesEveryOpenGroupWithoutOrders()
    {
        Result<CloseAcademicYearGroupsResponse> result = await CloseGroups(false);

        Assert.Equal([1, 2, 3], result.Value.Groups.Select(g => g.GrpId));
        Assert.All(StoredVoidDates(), g => Assert.Equal(CloseDate, g.VoidDate));
        Assert.Empty(StoredCloseOrders());
    }

    //D197: შემოწმება ბრძანებებს არ ქმნის და ჯგუფებს არ ცვლის; ჯგუფი 3 საწყისის გარეშეა და არ ჩამოითვლება
    [Fact]
    public async Task CloseGroups_OrdersModeDryRun_PlansOnlyGroupsWithOrdersAndChangesNothing()
    {
        await SwitchToOrders();

        Result<CloseAcademicYearGroupsResponse> result = await CloseGroups(true);

        CloseAcademicYearGroupsResponse response = result.Value;
        Assert.Equal((true, CloseDate), (response.DryRun, response.CloseDate));
        Assert.Equal([1, 2], response.Groups.Select(g => g.GrpId));
        //გეგმა დახურულ ჯგუფს ეხება: გაკვეთილები მხოლოდ დახურვის დღემდე
        Assert.All(response.Groups, g => Assert.True(g.Generation.CreatedLessonsCount > 0));
        Assert.All(response.Groups.SelectMany(g => g.Generation.Changes), c => Assert.True(c.LessonDt < CloseDate));
        Assert.All(StoredVoidDates(), g => Assert.Null(g.VoidDate));
        Assert.Empty(StoredCloseOrders());
        Assert.Empty(StoredLessonDays(1));
    }

    //D197: თითო ჯგუფს ბრძანება 7 დახურვის დღით და დახურული წლის ნომრით (26.1, 26.2), C2-ის გზით; გენერატორი იმავე
    //ტრანზაქციაში მუშაობს. ჯგუფი 3 ღია რჩება (D202)
    [Fact]
    public async Task CloseGroups_OrdersMode_ClosesEveryGroupWithOrdersByOrder7()
    {
        await SwitchToOrders();

        Result<CloseAcademicYearGroupsResponse> result = await CloseGroups(false);

        Assert.Equal([1, 2], result.Value.Groups.Select(g => g.GrpId));
        Assert.Equal([(1, (DateTime?)CloseDate), (2, CloseDate), (3, null)], StoredVoidDates());
        List<GroupOrder> orders = StoredCloseOrders();
        Assert.Equal(
        [
            ((int?)1, CloseDate, 11, (int?)26, (int?)1, (DateTime?)Day(11, 24), "admin"),
            (2, CloseDate, 11, 26, 2, Day(11, 24), "admin")
        ], orders.Select(o => (o.GroupId, o.OrderDate, o.AcademicYearId, o.NumberYear, o.Number, o.IssueDate,
            o.CreatedByUserName)));
        Assert.All(orders, o => Assert.Equal(("ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია"),
            (o.OrganizationName, o.SignerTitle, o.SignerName)));
        Assert.NotEmpty(StoredLessonDays(1));
        Assert.All(StoredLessonDays(1), day => Assert.True(day < CloseDate));
    }

    //D220: ჯგუფ 2-ს დახურვის შემდეგ ბრძანება აქვს (26.1, 21.12-ის გაყვანა), ამიტომ არცერთი ჯგუფი არ იხურება
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CloseGroups_OrdersModeGroupThatCannotBeClosed_ClosesNone(bool dryRun)
    {
        await SwitchToOrders();
        await CreateOrder(StudentsRequest(EGroupOrderType.RemoveStudents, 2, Day(12, 21), Removal(22)));

        Result<CloseAcademicYearGroupsResponse> result = await CloseGroups(dryRun);

        Assert.Equal(nameof(GroupOrderErrors.GroupsCannotBeClosed), result.Error.Code);
        Assert.Equal(GroupOrderErrors.GroupsCannotBeClosed(["606 — " + GroupOrderErrors.OrderNoLongerFits("26.1")
            .Description]), result.Error);
        Assert.All(StoredVoidDates(), g => Assert.Null(g.VoidDate));
        Assert.Empty(StoredCloseOrders());
    }
}
