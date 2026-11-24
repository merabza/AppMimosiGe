using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//რომელ ჯგუფს შეიძლება ბრძანება (D202)
public sealed class GroupCanHaveOrdersRuleTests
{
    private const int GroupId = 42;
    private const int TargetId = 43;

    private static readonly GroupOrderData GroupInitial = Creating(1, EGroupOrderType.Initial, GroupId);
    private static readonly GroupOrderData TargetCreated = Creating(2, EGroupOrderType.CreateGroup, TargetId);

    private static GroupOrderData Creating(int id, EGroupOrderType orderType, int? groupId, bool isPaused = false)
    {
        return new GroupOrderData
        {
            Id = id, OrderType = orderType, OrderDate = Day(9, 1), GroupId = groupId, IsPaused = isPaused
        };
    }

    private static GroupOrderData Transfer(int id)
    {
        return Order(id, EGroupOrderType.TransferStudents, Day(10, 1), GroupId) with { TargetGroupId = TargetId };
    }

    //D202: ბრძანებები 2–10 ჯგუფს, რომელსაც საწყისი ან გააქტიურებული შექმნის ბრძანება აქვს
    [Theory]
    [InlineData(EGroupOrderType.ChangeTeacher, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.AddStudents, EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.RemoveStudents, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.ChangeSchedule, EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.CloseGroup, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.ChangeStudentFees, EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.ChangeWeeklyHours, EGroupOrderType.CreateGroup)]
    public void Check_GroupWithACreatingOrder_CanHaveOrders(EGroupOrderType orderType, EGroupOrderType creatingType)
    {
        GroupOrderData order = Order(5, orderType, Day(10, 1), GroupId);

        Assert.Null(GroupCanHaveOrdersRule.Check(order, [Creating(1, creatingType, GroupId), order]));
    }

    //D202, D196: ჯგუფი, რომელმაც საწყისი ბრძანება არ მიიღო, ბრძანებით აღარ იცვლება; სხვა ჯგუფის ბრძანება არ ითვლება
    [Fact]
    public void Check_GroupWithoutACreatingOrder_CannotHaveOrders()
    {
        GroupOrderData order = Order(5, EGroupOrderType.AddStudents, Day(10, 1), GroupId);

        Assert.Equal(GroupOrderErrors.GroupCannotHaveOrders, GroupCanHaveOrdersRule.Check(order, [TargetCreated]));
    }

    //D186: დაპაუზებული შექმნის ბრძანება ჯგუფს ჯერ არ ქმნის
    [Fact]
    public void Check_GroupWithAPausedCreateOrder_CannotHaveOrders()
    {
        GroupOrderData order = Order(5, EGroupOrderType.AddStudents, Day(10, 1), GroupId);

        Assert.Equal(GroupOrderErrors.GroupCannotHaveOrders,
            GroupCanHaveOrdersRule.Check(order, [Creating(1, EGroupOrderType.CreateGroup, GroupId, true)]));
    }

    //D202: გადაყვანისას სამიზნესაც სჭირდება საწყისი ან შექმნის ბრძანება; ჯერ „საიდან“ მოწმდება
    [Fact]
    public void Check_Transfer_NeedsBothGroups()
    {
        Assert.Null(GroupCanHaveOrdersRule.Check(Transfer(5), [GroupInitial, TargetCreated]));
        Assert.Equal(GroupOrderErrors.TargetGroupCannotHaveOrders,
            GroupCanHaveOrdersRule.Check(Transfer(5), [GroupInitial]));
        Assert.Equal(GroupOrderErrors.GroupCannotHaveOrders, GroupCanHaveOrdersRule.Check(Transfer(5), []));
    }

    //D202: შექმნის ბრძანება ახალ ჯგუფს ქმნის; შეცვლისას ის თავისი ჯგუფის შექმნის ბრძანებაა
    [Fact]
    public void Check_CreateOrderOfANewGroup_IsAllowed()
    {
        GroupOrderData order = Creating(5, EGroupOrderType.CreateGroup, GroupId + 10);

        Assert.Null(GroupCanHaveOrdersRule.Check(order, [GroupInitial, TargetCreated]));
        Assert.Null(GroupCanHaveOrdersRule.Check(TargetCreated, [GroupInitial, TargetCreated]));
    }

    //D202: ჯგუფს, რომელსაც უკვე აქვს საწყისი ან შექმნის ბრძანება, მეორე არ ექმნება
    [Theory]
    [InlineData(EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.CreateGroup)]
    public void Check_SecondCreatingOrderOfAGroup_IsRefused(EGroupOrderType orderType)
    {
        Assert.Equal(GroupOrderErrors.CreateOrderCannotHaveGroup,
            GroupCanHaveOrdersRule.Check(Creating(5, orderType, GroupId), [GroupInitial]));
    }

    //დაპაუზებულ შექმნის ბრძანებას ჯგუფი ჯერ არ აქვს
    [Fact]
    public void Check_PausedCreateOrder_IsAllowed()
    {
        Assert.Null(GroupCanHaveOrdersRule.Check(Creating(5, EGroupOrderType.CreateGroup, null, true),
            [GroupInitial, TargetCreated]));
    }
}
