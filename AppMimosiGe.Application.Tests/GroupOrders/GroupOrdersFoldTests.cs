using System;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrdersFoldTests
{
    private const int GroupId = 42;

    //ბრძანება 0: ველები, გაუქმების თარიღი და სტრიქონები თავიანთი თარიღებით. მასწავლებელი 01.10-ს შეიცვალა, მეორე
    //მოსწავლე 15.10-ს გავიდა, განრიგი ბრძანების თარიღზე (ჯგუფის პირველ მოქმედ დღეზე) ადრე დაიწყო
    private static GroupOrderData InitialOrder(int id, DateTime orderDate, DateTime? voidDate = null)
    {
        return new GroupOrderData
        {
            Id = id,
            OrderType = EGroupOrderType.Initial,
            OrderDate = orderDate,
            GroupId = GroupId,
            Fields = Fields,
            VoidDate = voidDate,
            Teachers =
            [
                new GroupOrderTeacherLine(11, 5, 8, Day(9, 2), Day(10, 1)),
                new GroupOrderTeacherLine(12, 6, 9, Day(10, 1))
            ],
            Students =
            [
                new GroupOrderStudentLine(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა", Day(9, 2)),
                new GroupOrderStudentLine(22, 30, 8f, 48m, 6m, 1f, null, Day(9, 2), Day(10, 15))
            ],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(31, 1, 4, 2f, 1, Day(9, 1))]
        };
    }

    //ბრძანება 1: მასწავლებელი, ორი მოსწავლე და განრიგი, თარიღების გარეშე
    private static GroupOrderData CreateOrder(int id, DateTime orderDate)
    {
        return new GroupOrderData
        {
            Id = id,
            OrderType = EGroupOrderType.CreateGroup,
            OrderDate = orderDate,
            GroupId = GroupId,
            Fields = Fields,
            Teachers = [new GroupOrderTeacherLine(11, 5, 8)],
            Students =
            [
                new GroupOrderStudentLine(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა"),
                new GroupOrderStudentLine(22, 30, 8f, 48m, 6m, 1f)
            ],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(31, 1, 4, 2f, 1)]
        };
    }

    //D186: მასწავლებლის გარეშე შექმნის ბრძანება დაპაუზებულია და ჯგუფი ჯერ არ აქვს
    private static GroupOrderData PausedCreateOrder(int id, DateTime orderDate)
    {
        return CreateOrder(id, orderDate) with { IsPaused = true, GroupId = null, Teachers = [] };
    }

    private static GroupOrderData CloseOrder(int id, DateTime orderDate)
    {
        return new GroupOrderData
        {
            Id = id, OrderType = EGroupOrderType.CloseGroup, OrderDate = orderDate, GroupId = GroupId
        };
    }

    private static GroupOrderData Creating(int id, EGroupOrderType orderType, DateTime orderDate)
    {
        return orderType == EGroupOrderType.Initial ? InitialOrder(id, orderDate) : CreateOrder(id, orderDate);
    }

    //ბრძანებები ესადაგება და ჯგუფს ქმნის
    private static GroupState Fold(params GroupOrderData[] orders)
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run(orders);
        Assert.Null(result.NotFitting);
        return result.States[GroupId];
    }

    private static GroupOrderNotFitting? NotFitting(params GroupOrderData[] orders)
    {
        return GroupOrdersFold.Run(orders).NotFitting;
    }

    //D196: 0 წერს ველებს და გაუქმების თარიღს, სტრიქონებს კი თავიანთი თარიღებით ქმნის, დასრულებულებსაც; ყოველ
    //სტრიქონს თავისი ხაზი ახლავს
    [Fact]
    public void Run_Initial_CreatesTheRowsWithTheirOwnDates()
    {
        GroupState state = Fold(InitialOrder(1, Day(9, 2), Day(12, 31)));

        Assert.Equal(Fields, state.Fields);
        Assert.Equal(Day(12, 31), state.VoidDate);
        Assert.Equal(
            [new GroupTeacherRow(11, 5, 8, Day(9, 2), Day(10, 1)), new GroupTeacherRow(12, 6, 9, Day(10, 1), null)],
            state.Teachers);
        Assert.Equal(
        [
            new GroupStudentRow(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა", Day(9, 2), null),
            new GroupStudentRow(22, 30, 8f, 48m, 6m, 1f, null, Day(9, 2), Day(10, 15))
        ], state.Students);
        Assert.Equal([new GroupDayTimePlaceRow(31, 1, 4, 2f, 1, Day(9, 1), null)], state.DayTimePlaces);
    }

    //D186: 1 წერს ველებს და ყველა სტრიქონს ბრძანების თარიღიდან იწყებს; ჯგუფი ამ დღიდან მოქმედია
    [Fact]
    public void Run_CreateGroup_CreatesTheFieldsAndEveryRowFromTheOrderDate()
    {
        GroupState state = Fold(CreateOrder(1, Day(9, 15)));

        Assert.Equal(Fields, state.Fields);
        Assert.Null(state.VoidDate);
        Assert.Equal([new GroupTeacherRow(11, 5, 8, Day(9, 15), null)], state.Teachers);
        Assert.Equal(
        [
            new GroupStudentRow(21, 20, 12f, 72m, 6m, 1.5f, "შენიშვნა", Day(9, 15), null),
            new GroupStudentRow(22, 30, 8f, 48m, 6m, 1f, null, Day(9, 15), null)
        ], state.Students);
        Assert.Equal([new GroupDayTimePlaceRow(31, 1, 4, 2f, 1, Day(9, 15), null)], state.DayTimePlaces);
        Assert.Equal(Day(9, 15), GroupActivity.FirstActiveDay(state));
    }

    //D190: 7 მხოლოდ VoidDate = a-ს წერს: აქ ჯგუფი 0-ის გაუქმების თარიღზე ადრე იხურება, სტრიქონები უცვლელია
    [Fact]
    public void Run_CloseGroup_WritesOnlyTheVoidDate()
    {
        GroupState initial = Fold(InitialOrder(1, Day(9, 2), Day(12, 31)));

        GroupState closed = Fold(InitialOrder(1, Day(9, 2), Day(12, 31)), CloseOrder(2, Day(11, 20)));

        Assert.Equal(Day(11, 20), closed.VoidDate);
        Assert.Equal(initial.Fields, closed.Fields);
        Assert.Equal(initial.Teachers, closed.Teachers);
        Assert.Equal(initial.Students, closed.Students);
        Assert.Equal(initial.DayTimePlaces, closed.DayTimePlaces);
    }

    //D186: დაპაუზებული შექმნის ბრძანება ჯგუფს არ ქმნის
    [Fact]
    public void Run_PausedCreateGroup_CreatesNoGroup()
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run([PausedCreateOrder(1, Day(9, 15))]);

        Assert.Null(result.NotFitting);
        Assert.Empty(result.States);
    }

    //დაპაუზებული ბრძანება ეფექტს არ იძლევა, ამიტომ მის შემდეგ დასახური ჯგუფი არ არის
    [Fact]
    public void Run_PausedCreateGroup_LaterOrderDoesNotFit()
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.OrderIsBeforeGroupStart),
            NotFitting(PausedCreateOrder(1, Day(9, 15)), CloseOrder(2, Day(11, 20))));
    }

    //D185: ერთი დღის ბრძანებები Id-ის რიგით გაივლის: ჯერ შექმნა (3), მერე დახურვა (8), თუმცა სიაში პირიქითაა
    [Fact]
    public void Run_SameDay_OrdersGoByTheirIds()
    {
        GroupState state = Fold(CloseOrder(8, Day(9, 15)), CreateOrder(3, Day(9, 15)));

        Assert.Equal(Day(9, 15), state.VoidDate);
    }

    //დახურვა (4) იმავე დღის შექმნაზე (9) ადრე შეიქმნა, ამიტომ ჯგუფის შექმნამდე გადის და არ ესადაგება
    [Fact]
    public void Run_SameDay_LowerIdGoesFirst()
    {
        Assert.Equal(new GroupOrderNotFitting(4, GroupOrderErrors.OrderIsBeforeGroupStart),
            NotFitting(CreateOrder(9, Day(9, 15)), CloseOrder(4, Day(9, 15))));
    }

    //თარიღი Id-ზე წინაა: შექმნა (5) დახურვაზე (2) ერთი დღით ადრეა
    [Fact]
    public void Run_TheDateGoesBeforeTheId()
    {
        GroupState state = Fold(CloseOrder(2, Day(9, 16)), CreateOrder(5, Day(9, 15)));

        Assert.Equal(Day(9, 16), state.VoidDate);
    }

    //D190: ჯგუფის დაწყებამდე (0-ის ან 1-ის თარიღამდე) ბრძანება არ ესადაგება, თუმცა ის მოგვიანებით შეიქმნა
    [Theory]
    [InlineData(EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.CreateGroup)]
    public void Run_OrderBeforeTheGroupStart_DoesNotFit(EGroupOrderType creatingType)
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.OrderIsBeforeGroupStart),
            NotFitting(Creating(1, creatingType, Day(9, 15)), CloseOrder(2, Day(9, 14))));
    }

    //D190: ჯგუფის დაწყებამდე B2-ის ტიპებიც არ ესადაგება, ხაზების შემოწმებამდე; გადაყვანისას „საიდან“ ჯგუფი
    [Theory]
    [InlineData(EGroupOrderType.ChangeTeacher)]
    [InlineData(EGroupOrderType.AddStudents)]
    [InlineData(EGroupOrderType.RemoveStudents)]
    [InlineData(EGroupOrderType.TransferStudents)]
    [InlineData(EGroupOrderType.ChangeStudentFees)]
    [InlineData(EGroupOrderType.ChangeSalaryScheme)]
    public void Run_StudentOrTeacherOrderBeforeTheGroupStart_DoesNotFit(EGroupOrderType orderType)
    {
        GroupOrderData order = Order(2, orderType, Day(9, 14), GroupId) with
        {
            TargetGroupId = orderType == EGroupOrderType.TransferStudents ? GroupId + 1 : null
        };

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.OrderIsBeforeGroupStart),
            NotFitting(CreateOrder(1, Day(9, 15)), CreateOrder(3, Day(9, 1)) with { GroupId = GroupId + 1 }, order));
    }

    //D202: ჯგუფს ერთხელ ქმნის საწყისი ან შექმნის ბრძანება
    [Theory]
    [InlineData(EGroupOrderType.Initial, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.Initial, EGroupOrderType.CreateGroup)]
    [InlineData(EGroupOrderType.CreateGroup, EGroupOrderType.Initial)]
    [InlineData(EGroupOrderType.CreateGroup, EGroupOrderType.CreateGroup)]
    public void Run_SecondOrderCreatingTheGroup_DoesNotFit(EGroupOrderType first, EGroupOrderType second)
    {
        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.CreateOrderCannotHaveGroup),
            NotFitting(Creating(1, first, Day(9, 2)), Creating(2, second, Day(10, 1))));
    }

    //გატარება პირველივე შეუსაბამო ბრძანებაზე ჩერდება
    [Fact]
    public void Run_SeveralOrdersDoNotFit_ReturnsTheFirst()
    {
        Assert.Equal(new GroupOrderNotFitting(5, GroupOrderErrors.OrderIsBeforeGroupStart),
            NotFitting(CloseOrder(4, Day(9, 2)), CloseOrder(5, Day(9, 1))));
    }

    [Fact]
    public void Run_NoOrders_CreatesNoGroup()
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run([]);

        Assert.Null(result.NotFitting);
        Assert.Empty(result.States);
    }

    //ყოველ ჯგუფს თავისი ბრძანებები და მდგომარეობა აქვს: 43-ის დახურვა 42-ს არ ეხება
    [Fact]
    public void Run_SeveralGroups_EachGetsItsOwnOrders()
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run([
            CreateOrder(1, Day(9, 15)), CreateOrder(2, Day(9, 15)) with { GroupId = GroupId + 1 },
            CloseOrder(3, Day(11, 20)) with { GroupId = GroupId + 1 }
        ]);

        Assert.Null(result.NotFitting);
        Assert.Equal([GroupId, GroupId + 1], result.States.Keys.Order());
        Assert.Null(result.States[GroupId].VoidDate);
        Assert.Equal(Day(11, 20), result.States[GroupId + 1].VoidDate);
    }

    //შეუსაბამო ბრძანებისას მდგომარეობა არცერთ ჯგუფს არ აქვს, იმასაც, რომლის ბრძანებები ესადაგება
    [Fact]
    public void Run_OrderDoesNotFit_ReturnsNoStates()
    {
        GroupOrdersFoldResult result = GroupOrdersFold.Run([
            CreateOrder(1, Day(9, 15)), CloseOrder(2, Day(9, 1)) with { GroupId = GroupId + 1 }
        ]);

        Assert.Equal(new GroupOrderNotFitting(2, GroupOrderErrors.OrderIsBeforeGroupStart), result.NotFitting);
        Assert.Empty(result.States);
    }

    //ჯგუფის გარეშე მხოლოდ დაპაუზებული ბრძანებაა (D186)
    [Fact]
    public void Run_ActiveOrderWithoutAGroup_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            GroupOrdersFold.Run([CreateOrder(1, Day(9, 15)) with { GroupId = null }]));
    }

    //გადაყვანა ორ ჯგუფს ცვლის, ამიტომ ერთი ჯგუფის Apply მას არ იღებს
    [Fact]
    public void Apply_Transfer_Throws()
    {
        GroupOrderData transfer = Order(2, EGroupOrderType.TransferStudents, Day(10, 1), GroupId) with
        {
            TargetGroupId = GroupId + 1
        };

        Assert.Throws<ArgumentException>(() => GroupOrderEffects.Apply(Fold(CreateOrder(1, Day(9, 15))), transfer));
    }
}
