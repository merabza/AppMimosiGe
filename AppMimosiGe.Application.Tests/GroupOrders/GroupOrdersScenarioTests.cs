using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.CreateGroupOrder;
using AppMimosiGe.Application.GroupOrders.DeleteGroupOrder;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.Rights;
using AppMimosiGe.Infrastructure.Repositories;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcass.Application.Identity;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Domain.Models;
using MimosiGeDbPart.Db;
using Moq;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrderRequests;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//სრული სცენარი ნამდვილ ვალიდატორებზე, handler-ებსა და რეპოზიტორიებზე, EF InMemory ბაზაზე: ჯგუფის შექმნა, მოსწავლის
//დამატება, მეორე ჯგუფი, გადაყვანა და ადრეული თარიღის ბრძანება, რომლის შემდეგაც გადაყვანა თავიდან თამაშდება; მერე
//წაშლის უარი და წაშლა. ყოველი ნაბიჯი ახალი კონტექსტითაა, როგორც ცალკე მოთხოვნა. დღეს 25.11.2026, გადასვლის დღე
//24.11.2026. მხოლოდ სინთეზური მონაცემები
public sealed class GroupOrdersScenarioTests
{
    private readonly Mock<IUserClaimRights> _claimRights = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly DbContextOptions<MimosiGeDbContext> _options = new DbContextOptionsBuilder<MimosiGeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private readonly TimeProvider _timeProvider;

    public GroupOrdersScenarioTests()
    {
        _currentUser.Setup(u => u.Name).Returns("manager");
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        _timeProvider = timeProvider.Object;
        Seed();
    }

    //ბრძანებების რეჟიმი, სასწავლო წელი 11 და ცნობარები: მასწავლებლები 5 და 6 (სქემა 8), მოსწავლეები 20–22, ორშაბათი და
    //ოთხშაბათი, დრო 4 (15:00), ოთახი 1
    private void Seed()
    {
        using MimosiGeDbContext context = NewContext();
        context.GroupOrdersMode.Add(new GroupOrdersMode
        {
            Id = GroupOrdersMode.SingleRowId, IsOrdersMode = true, TransitionDate = Day(11, 24)
        });
        context.AcademicYears.Add(new AcademicYear
        {
            AyId = 11, AcademicYearName = "2026-2027", StartDate = Day(9, 1), FinishDate = Day(9, 1).AddYears(1)
        });
        context.Courses.Add(new Course { CrsId = 6, CourseName = "მათემატიკა" });
        context.GroupSizes.Add(new GroupSize { GrsId = 2, GrsSize = 2, GrsName = "2" });
        context.StudentStatuses.Add(new StudentStatus { Id = 10, StudentStatusName = "მე-6 კლასი", Rate = 106 });
        context.TeacherSalarySchemes.Add(new TeacherSalaryScheme { Id = 8, SchemaName = "საათობრივი" });
        context.TeacherContracts.AddRange(
            new TeacherContract { Id = 5, ContractNumber = "T5", TeacherHumanId = 1, SalarySchemaByHoursId = 8 },
            new TeacherContract { Id = 6, ContractNumber = "T6", TeacherHumanId = 2, SalarySchemaByHoursId = 8 });
        context.StudentContracts.AddRange(StudentContract(20), StudentContract(21), StudentContract(22));
        context.WeekDays.AddRange(new WeekDay { Id = 1, Name = "ორშაბათი", ShortName = "ორშ", WeekDayNumber = 1 },
            new WeekDay { Id = 3, Name = "ოთხშაბათი", ShortName = "ოთხ", WeekDayNumber = 3 });
        context.LessonStartTimes.Add(new LessonStartTime { LstId = 4, LstTime = new TimeOnly(15, 0) });
        context.Rooms.Add(new Room { Id = 1, RoomName = "1" });
        context.OperationMonths.Add(new OperationMonth { MonthDate = Day(12, 1) });
        context.SaveChanges();
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

    private MimosiGeDbContext NewContext()
    {
        return new MimosiGeDbContext(_options, true);
    }

    //carcass-ის SaveChangesAsync-ს domain events dispatcher სჭირდება, ამიტომ კონტექსტი სინქრონულად ინახება
    private static IUnitOfWork UnitOfWork(MimosiGeDbContext context)
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => context.SaveChanges())
            .Returns(Task.CompletedTask);
        return unitOfWork.Object;
    }

    //ბრძანების შექმნა ისე, როგორც API-დან: ჯერ ვალიდატორი (ValidationDecorator), მერე handler-ი
    private async Task<int> Create(GroupOrderRequest request)
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
            _timeProvider, new GroupOrdersSettings()).Handle(command, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        return result.Value;
    }

    private async Task<Result> Delete(int orderId)
    {
        await using MimosiGeDbContext context = NewContext();
        var groupsRepository = new GroupsRepository(context);
        return await new DeleteGroupOrderCommandHandler(new GroupOrdersRepository(context), groupsRepository,
                new GroupRequestValidator(groupsRepository, new StudentContractsRepository(context),
                    new TeacherContractsRepository(context)), new LessonGeneratorRepository(context),
                UnitOfWork(context), _claimRights.Object, _timeProvider)
            .Handle(new DeleteGroupOrderCommand(orderId), CancellationToken.None);
    }

    private int StoredGroupId(string groupCode)
    {
        using MimosiGeDbContext context = NewContext();
        return context.Groups.Single(g => g.GroupCode == groupCode).GrpId;
    }

    //ჯგუფის მოსწავლეების სტრიქონები: კონტრაქტი, პერიოდი, საათის ფასი და ხაზის ბრძანება
    private List<(int StudentContractId, DateTime StartDate, DateTime? EndDate, decimal OneHourFee, int OrderId)>
        StoredStudentRows(int groupId)
    {
        using MimosiGeDbContext context = NewContext();
        return
        [
            .. context.GroupsByStudents.Where(s => s.GroupId == groupId).Include(s => s.GroupOrderStudent).ToList()
                .OrderBy(s => s.StudentContractId).ThenBy(s => s.StartDate).Select(s => (s.StudentContractId,
                    s.StartDate, s.EndDate, s.OneHourFee, s.GroupOrderStudent!.GroupOrderId))
        ];
    }

    private List<(int Id, int? Number)> StoredNumbers()
    {
        using MimosiGeDbContext context = NewContext();
        return [.. context.GroupOrders.OrderBy(o => o.Id).ToList().Select(o => (o.Id, o.Number))];
    }

    [Fact]
    public async Task OrdersFromCreationToTransfer_AreReplayedIntoTheGroupRows()
    {
        //ჯგუფი A ("605"): მასწავლებელი 5, ორშაბათი 15:00, მოსწავლე 20, 30.11-იდან; 07.12-იდან მოსწავლე 21
        int createA = await Create(CreateGroupRequest(Day(11, 30), [Teacher()], [Student(20)], [Monday()]));
        int groupA = StoredGroupId("605");
        int add = await Create(StudentsRequest(EGroupOrderType.AddStudents, groupA, Day(12, 7), Student(21)));

        //ჯგუფი B ("606"): მასწავლებელი 6, ოთხშაბათი 15:00, მოსწავლე 22; 14.12-იდან 21 A-დან B-ში გადადის
        int createB = await Create(CreateGroupRequest(Day(11, 30), [Teacher(6)], [Student(22)],
            [Monday(weekDayId: 3)], "606"));
        int groupB = StoredGroupId("606");
        int transfer = await Create(TransferRequest(groupA, groupB, Day(12, 14), Student(21)));

        //ადრეული თარიღის ბრძანება: 21-ის საათის ფასი A-ში 10.12-იდან 7 ლარია. გადაყვანა თავიდან თამაშდება და A-ში
        //უკვე ამ სტრიქონს ასრულებს
        int fee = await Create(StudentsRequest(EGroupOrderType.ChangeStudentFees, groupA, Day(12, 10),
            Student(21, 7m)));

        Assert.Equal(
        [
            (20, Day(11, 30), (DateTime?)null, 6m, createA), (21, Day(12, 7), Day(12, 10), 6m, add),
            (21, Day(12, 10), Day(12, 14), 7m, fee)
        ], StoredStudentRows(groupA));
        Assert.Equal([(21, Day(12, 14), (DateTime?)null, 6m, transfer), (22, Day(11, 30), null, 6m, createB)],
            StoredStudentRows(groupB));
        Assert.Equal([(createA, (int?)1), (add, 2), (createB, 3), (transfer, 4), (fee, 5)], StoredNumbers());

        //D191: B-ს გადაყვანა აკავშირებს A-სთან, ამიტომ მისი შექმნის ბრძანება არ იშლება
        Result refused = await Delete(createB);
        Assert.Equal(nameof(GroupOrderErrors.CreateOrderHasTransfers), refused.Error.Code);

        //გადაყვანის წაშლა: A-ში 21-ის სტრიქონი ისევ ღიაა, B-ში მისი სტრიქონი იშლება; ნომერი 4 თავიდან არ გაიცემა
        Result deleted = await Delete(transfer);
        Assert.True(deleted.IsSuccess);
        Assert.Equal(
        [
            (20, Day(11, 30), (DateTime?)null, 6m, createA), (21, Day(12, 7), Day(12, 10), 6m, add),
            (21, Day(12, 10), null, 7m, fee)
        ], StoredStudentRows(groupA));
        Assert.Equal([(22, Day(11, 30), (DateTime?)null, 6m, createB)], StoredStudentRows(groupB));

        int close = await Create(CloseRequest(groupB, Day(12, 21)));
        Assert.Equal([(createA, (int?)1), (add, 2), (createB, 3), (fee, 5), (close, 6)], StoredNumbers());
        await using MimosiGeDbContext context = NewContext();
        Group storedB = await context.Groups.SingleAsync(g => g.GrpId == groupB);
        Assert.Equal(Day(12, 21), storedB.VoidDate);
        Assert.True(storedB.DirtyLessons);
        GroupOrder storedFee =
            await context.GroupOrders.Include(o => o.GroupOrderStudents).SingleAsync(o => o.Id == fee);
        Assert.Equal((groupA, 11, 8f), (storedFee.GroupId!.Value, storedFee.AcademicYearId,
            storedFee.GroupOrderStudents.Single().FourWeekHours!.Value));
    }
}
