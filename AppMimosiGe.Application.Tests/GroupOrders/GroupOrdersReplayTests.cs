using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//გადათამაშება (D185): ადრეული ბრძანების ჩამატება, შეცვლა და წაშლა, გადაყვანების ჯაჭვი და შეხებული ჯგუფები
public sealed class GroupOrdersReplayTests
{
    private const int A = 42;
    private const int B = 43;
    private const int C = 44;
    private const int D = 45;

    //A: მოსწავლეები 20 (ხაზი 21) და 30 (ხაზი 22); B: 40 (ხაზი 31); C: 50 (ხაზი 41); ყველა 01.09-იდან
    private static readonly GroupOrderData AInitial =
        InitialOrder(1, A, InitialStudentLine(21, 20), InitialStudentLine(22, 30));

    private static readonly GroupOrderData BInitial = InitialOrder(2, B, InitialStudentLine(31, 40));
    private static readonly GroupOrderData CInitial = InitialOrder(3, C, InitialStudentLine(41, 50));

    //მოსწავლე 20 გადადის A-დან B-ში 01.10-ს (ბრძანება 10) და B-დან C-ში 01.11-ს (ბრძანება 11)
    private static readonly GroupOrderData AToB = Transfer(10, 10, 1, A, B, StudentLine(71, 20));
    private static readonly GroupOrderData BToC = Transfer(11, 11, 1, B, C, StudentLine(72, 20));

    private static GroupOrderData Students(int id, EGroupOrderType orderType, int month, int day, int groupId,
        params GroupOrderStudentLine[] lines)
    {
        return StudentsOrder(id, orderType, Day(month, day), groupId, lines);
    }

    private static GroupOrderData Transfer(int id, int month, int day, int sourceGroupId, int targetGroupId,
        params GroupOrderStudentLine[] lines)
    {
        return StudentsOrder(id, EGroupOrderType.TransferStudents, Day(month, day), sourceGroupId, lines) with
        {
            TargetGroupId = targetGroupId
        };
    }

    //ბრძანება ხაზების გარეშე: შეხებულ ჯგუფებს მხოლოდ ჯგუფები და ადგილი სჭირდება
    private static GroupOrderData At(int id, int month, int day, int groupId)
    {
        return Order(id, EGroupOrderType.ChangeStudentFees, Day(month, day), groupId);
    }

    private static int[] Affected(GroupOrderData? before, GroupOrderData? after, params GroupOrderData[] orders)
    {
        return [.. GroupOrdersReplay.AffectedGroupIds(before, after, orders).Order()];
    }

    private static GroupOrdersReplayResult Fits(GroupOrderData? before, GroupOrderData? after,
        params GroupOrderData[] orders)
    {
        GroupOrdersReplayResult result = GroupOrdersReplay.Run(before, after, orders);
        Assert.Null(result.NotFitting);
        return result;
    }

    //ადრეული ბრძანების ჩამატება: 15.10-ს გაყვანილ მოსწავლეს 01.11-ის ბრძანება 8 ვეღარ პოულობს
    [Fact]
    public void Run_InsertedEarlierRemoval_LaterFeeChangeNoLongerFits()
    {
        GroupOrderData feeChange = Students(10, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(61, 20, 7m));
        GroupOrderData removal = Students(12, EGroupOrderType.RemoveStudents, 10, 15, A, RemovalLine(62, 20));

        GroupOrdersReplayResult result = GroupOrdersReplay.Run(null, removal, [AInitial, feeChange]);

        Assert.Equal([A], result.AffectedGroupIds);
        Assert.Equal(new GroupOrderNotFitting(10, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
        Assert.Empty(result.States);
    }

    //ადრეული ბრძანების ჩამატება, რომელიც გვიანდელს ესადაგება: გვიანდელი ახალ სტრიქონს ყოფს
    [Fact]
    public void Run_InsertedEarlierFeeChange_TheLaterOneSplitsTheNewRow()
    {
        GroupOrderData later = Students(10, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(61, 20, 8m));
        GroupOrderData earlier = Students(12, EGroupOrderType.ChangeStudentFees, 10, 15, A, StudentLine(62, 20, 7m));

        GroupOrdersReplayResult result = Fits(null, earlier, AInitial, later);

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(10, 15)), StudentLineRow(22, 30, Day(9, 1), null),
            StudentLineRow(62, 20, Day(10, 15), Day(11, 1), 7m), StudentLineRow(61, 20, Day(11, 1), null, 8m)
        ], result.States[A].Students);
    }

    //თარიღის შეცვლა: 15.11-ის გაყვანა 15.10-ზე გადადის, 01.11-ის ბრძანება 8-ის წინ
    [Fact]
    public void Run_OrderMovedEarlier_LaterOrderNoLongerFits()
    {
        GroupOrderData feeChange = Students(10, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(61, 20, 7m));
        GroupOrderData removal = Students(11, EGroupOrderType.RemoveStudents, 11, 15, A, RemovalLine(62, 20));

        GroupOrdersReplayResult result =
            GroupOrdersReplay.Run(removal, removal with { OrderDate = Day(10, 15) }, [AInitial, feeChange, removal]);

        Assert.Equal(new GroupOrderNotFitting(10, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //შიგთავსის შეცვლა: 01.10-ს 60-ის ნაცვლად 70 ემატება, 01.11-ის ბრძანება 8 კი 60-ს ეხება
    [Fact]
    public void Run_ChangedStudent_LaterOrderNoLongerFits()
    {
        GroupOrderData addition = Students(10, EGroupOrderType.AddStudents, 10, 1, A, StudentLine(51, 60));
        GroupOrderData feeChange = Students(11, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(61, 60, 7m));

        GroupOrdersReplayResult result = GroupOrdersReplay.Run(addition,
            addition with { Students = [StudentLine(51, 70)] }, [AInitial, addition, feeChange]);

        Assert.Equal(new GroupOrderNotFitting(11, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //წაშლა: დამატების გარეშე მოსწავლე, რომელსაც 01.11-ს გაიყვანენ, ჯგუფში არ არის
    [Fact]
    public void Run_DeletedAddition_LaterRemovalNoLongerFits()
    {
        GroupOrderData addition = Students(10, EGroupOrderType.AddStudents, 10, 1, A, StudentLine(51, 60));
        GroupOrderData removal = Students(11, EGroupOrderType.RemoveStudents, 11, 1, A, RemovalLine(62, 60));

        GroupOrdersReplayResult result = GroupOrdersReplay.Run(addition, null, [AInitial, addition, removal]);

        Assert.Equal(new GroupOrderNotFitting(11, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //წაშლა: გაყვანის გარეშე მოსწავლე ჯგუფშია, ამიტომ მისი 01.11-ის დაბრუნება აღარ ესადაგება
    [Fact]
    public void Run_DeletedRemoval_LaterReturnNoLongerFits()
    {
        GroupOrderData removal = Students(10, EGroupOrderType.RemoveStudents, 10, 1, A, RemovalLine(62, 20));
        GroupOrderData comeback = Students(11, EGroupOrderType.AddStudents, 11, 1, A, StudentLine(63, 20));

        GroupOrdersReplayResult result = GroupOrdersReplay.Run(removal, null, [AInitial, removal, comeback]);

        Assert.Equal(new GroupOrderNotFitting(11, GroupOrderErrors.StudentIsAlreadyInGroup), result.NotFitting);
    }

    //წაშლა, რომელსაც გვიანდელი ბრძანება ესადაგება: 01.11-ის ბრძანება 8 0-ის სტრიქონს ყოფს
    [Fact]
    public void Run_DeletedEarlierFeeChange_TheLaterOneSplitsTheInitialRow()
    {
        GroupOrderData earlier = Students(10, EGroupOrderType.ChangeStudentFees, 10, 1, A, StudentLine(61, 20, 7m));
        GroupOrderData later = Students(11, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(62, 20, 8m));

        GroupOrdersReplayResult result = Fits(earlier, null, AInitial, earlier, later);

        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(11, 1)), StudentLineRow(22, 30, Day(9, 1), null),
            StudentLineRow(62, 20, Day(11, 1), null, 8m)
        ], result.States[A].Students);
    }

    //C2-ისთვის: თუ თვითონ შესანახი ბრძანება არ ესადაგება, შედეგს მისი Id და მიზეზი აქვს
    [Fact]
    public void Run_ChangedOrderItselfDoesNotFit_ReturnsItsIdAndReason()
    {
        GroupOrderData removal = Students(12, EGroupOrderType.RemoveStudents, 10, 15, A, RemovalLine(62, 99));

        Assert.Equal(new GroupOrderNotFitting(12, GroupOrderErrors.StudentIsNotInGroup),
            GroupOrdersReplay.Run(null, removal, [AInitial]).NotFitting);
    }

    //ამოცანის მაგალითი: 15.10-ს გადაყვანილ მოსწავლეს A-ში 01.11-ის ბრძანება 8 ვეღარ პოულობს; შეხებულია ორივე ჯგუფი
    [Fact]
    public void Run_InsertedTransfer_LaterFeeChangeOfTheTransferredStudentNoLongerFits()
    {
        GroupOrderData feeChange = Students(10, EGroupOrderType.ChangeStudentFees, 11, 1, A, StudentLine(61, 20, 7m));

        GroupOrdersReplayResult result = GroupOrdersReplay.Run(null, Transfer(12, 10, 15, A, B, StudentLine(73, 20)),
            [AInitial, BInitial, feeChange]);

        Assert.Equal([A, B], result.AffectedGroupIds.Order());
        Assert.Equal(new GroupOrderNotFitting(10, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //ჯაჭვი სამ ჯგუფზე: A-ში 15.09-ის ბრძანება 8 ორივე გვიანდელ გადაყვანას და სამივე ჯგუფს ეხება
    [Fact]
    public void Run_TransferChainOnThreeGroups_ReplaysEveryGroup()
    {
        GroupOrderData feeChange = Students(12, EGroupOrderType.ChangeStudentFees, 9, 15, A, StudentLine(61, 20, 7m));

        GroupOrdersReplayResult result = Fits(null, feeChange, AInitial, BInitial, CInitial, AToB, BToC);

        Assert.Equal([A, B, C], result.AffectedGroupIds.Order());
        Assert.Equal(
        [
            StudentLineRow(21, 20, Day(9, 1), Day(9, 15)), StudentLineRow(22, 30, Day(9, 1), null),
            StudentLineRow(61, 20, Day(9, 15), Day(10, 1), 7m)
        ], result.States[A].Students);
        Assert.Equal([StudentLineRow(31, 40, Day(9, 1), null), StudentLineRow(71, 20, Day(10, 1), Day(11, 1))],
            result.States[B].Students);
        Assert.Equal([StudentLineRow(41, 50, Day(9, 1), null), StudentLineRow(72, 20, Day(11, 1), null)],
            result.States[C].Students);
    }

    //ჯაჭვი სამ ჯგუფზე: A-დან 15.09-ს გაყვანილ მოსწავლეს 01.10-ის გადაყვანა ვეღარ პოულობს
    [Fact]
    public void Run_TransferChainOnThreeGroups_EarlierRemovalBreaksTheFirstTransfer()
    {
        GroupOrderData removal = Students(12, EGroupOrderType.RemoveStudents, 9, 15, A, RemovalLine(62, 20));

        GroupOrdersReplayResult result =
            GroupOrdersReplay.Run(null, removal, [AInitial, BInitial, CInitial, AToB, BToC]);

        Assert.Equal([A, B, C], result.AffectedGroupIds.Order());
        Assert.Equal(new GroupOrderNotFitting(10, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //ჯაჭვი სამ ჯგუფზე: პირველი გადაყვანა მეორის შემდეგ გადადის, ამიტომ 01.11-ს მოსწავლე B-ში ჯერ არ არის
    [Fact]
    public void Run_TransferChainOnThreeGroups_FirstTransferMovedAfterTheSecond()
    {
        GroupOrdersReplayResult result = GroupOrdersReplay.Run(AToB, AToB with { OrderDate = Day(11, 15) },
            [AInitial, BInitial, CInitial, AToB, BToC]);

        Assert.Equal([A, B, C], result.AffectedGroupIds.Order());
        Assert.Equal(new GroupOrderNotFitting(11, GroupOrderErrors.StudentIsNotInGroup), result.NotFitting);
    }

    //D205: B-ში 01.11-ის ცვლილება ადრეულ გადაყვანას არ ეხება, ამიტომ შეხებულია მხოლოდ B. მისი სტრიქონის 15.12-ის
    //დასრულება კი A-ის 0-იდან მოდის, ამიტომ A-საც გატარება სჭირდება
    [Fact]
    public void Run_EarlierTransferFromAnUnaffectedGroup_TheTargetRowKeepsTheSourceEnd()
    {
        GroupOrderData aInitial = InitialOrder(1, A, InitialStudentLine(21, 20, Day(12, 15)));
        GroupOrderData feeChange = Students(12, EGroupOrderType.ChangeStudentFees, 11, 1, B, StudentLine(62, 20, 8m));

        GroupOrdersReplayResult result = Fits(null, feeChange, aInitial, BInitial, AToB);

        Assert.Equal([B], result.AffectedGroupIds);
        Assert.Equal([B], result.States.Keys);
        Assert.Equal(
        [
            StudentLineRow(31, 40, Day(9, 1), null), StudentLineRow(71, 20, Day(10, 1), Day(11, 1)),
            StudentLineRow(62, 20, Day(11, 1), Day(12, 15), 8m)
        ], result.States[B].Students);
    }

    //ახალი ჯგუფის შექმნის ბრძანება დროებითი ჯგუფის Id-ით
    [Fact]
    public void Run_NewCreateOrder_CreatesTheGroup()
    {
        GroupOrderData create = Order(100, EGroupOrderType.CreateGroup, Day(10, 1), -1) with
        {
            Fields = Fields,
            Teachers = [new GroupOrderTeacherLine(-11, 5, 8)],
            Students = [StudentLine(-12, 20)],
            DayTimePlaces = [new GroupOrderDayTimePlaceLine(-13, 1, 4, 2f, 1)]
        };

        GroupOrdersReplayResult result = Fits(null, create, AInitial);

        Assert.Equal([-1], result.AffectedGroupIds);
        Assert.Equal([StudentLineRow(-12, 20, Day(10, 1), null)], result.States[-1].Students);
    }

    //შექმნის ბრძანების წაშლის შემდეგ შეხებული ჯგუფი აღარ არსებობს
    [Fact]
    public void Run_DeletedOnlyInitialOrder_TheGroupIsGone()
    {
        GroupOrdersReplayResult result = Fits(AInitial, null, AInitial);

        Assert.Equal([A], result.AffectedGroupIds);
        Assert.Empty(result.States);
    }

    //შეხებულია ბრძანების ჯგუფი; ადრეული გადაყვანა მას არაფერს უკავშირებს
    [Fact]
    public void AffectedGroupIds_OrderOfOneGroup_IsThatGroup()
    {
        GroupOrderData order = At(20, 10, 15, A);

        Assert.Equal([A], Affected(null, order, order, Transfer(10, 10, 1, A, B)));
    }

    //გადაყვანისას შეხებულია ორივე ჯგუფი, წაშლისასაც
    [Fact]
    public void AffectedGroupIds_Transfer_IsBothGroups()
    {
        GroupOrderData transfer = Transfer(20, 10, 1, A, B);

        Assert.Equal([A, B], Affected(null, transfer, transfer));
        Assert.Equal([A, B], Affected(transfer, null));
    }

    //ჯაჭვი: ყოველი გვიანდელი გადაყვანა შემდეგ ჯგუფს უკავშირებს
    [Fact]
    public void AffectedGroupIds_LaterTransfers_LinkAChain()
    {
        GroupOrderData order = At(20, 9, 15, A);

        Assert.Equal([A, B, C, D],
            Affected(null, order, order, Transfer(10, 10, 1, A, B), Transfer(11, 11, 1, B, C),
                Transfer(12, 12, 1, C, D)));
    }

    //B შეხებულია 01.10-იდან, ამიტომ მისი 20.09-ის გადაყვანა C-ს არ უკავშირებს
    [Fact]
    public void AffectedGroupIds_ChainGoesOnlyThroughTransfersAfterTheLink()
    {
        GroupOrderData order = At(20, 9, 15, A);

        Assert.Equal([A, B], Affected(null, order, order, Transfer(10, 10, 1, A, B), Transfer(11, 9, 20, B, C)));
    }

    //ერთი დღის ბრძანებები Id-ის რიგით გაივლის: ცვლილებაზე (20) ადრე შექმნილი გადაყვანა (15) მას არ ეხება
    [Fact]
    public void AffectedGroupIds_SameDay_OnlyTransfersCreatedLaterLink()
    {
        GroupOrderData order = At(20, 10, 1, A);

        Assert.Equal([A, C], Affected(null, order, order, Transfer(15, 10, 1, A, B), Transfer(25, 10, 1, A, C)));
    }

    //სამიზნის შეცვლა: შეხებულია ძველი და ახალი სამიზნეც
    [Fact]
    public void AffectedGroupIds_ChangedTarget_IsTheOldAndTheNewTarget()
    {
        GroupOrderData before = Transfer(10, 10, 1, A, B);
        GroupOrderData after = before with { TargetGroupId = C };

        Assert.Equal([A, B, C], Affected(before, after, after));
    }

    //თარიღის შეცვლა: ჯაჭვი ორი თარიღიდან ადრეულით იწყება, ორივე მიმართულებით
    [Theory]
    [InlineData(11, 15, 10, 15)]
    [InlineData(10, 15, 11, 15)]
    public void AffectedGroupIds_MovedOrder_LinksFromTheEarlierDate(int beforeMonth, int beforeDay, int afterMonth,
        int afterDay)
    {
        GroupOrderData after = At(20, afterMonth, afterDay, A);

        Assert.Equal([A, B],
            Affected(At(20, beforeMonth, beforeDay, A), after, after, Transfer(10, 11, 1, A, B)));
    }

    //D-ს B-ის 20.10-ის გადაყვანა უკავშირებს: B პირდაპირ 01.11-იდანაა შეხებული, C-ის გავლით კი 15.10-იდან
    [Fact]
    public void AffectedGroupIds_EarlierLinkThroughAnotherGroup_ExtendsTheChain()
    {
        GroupOrderData order = At(20, 9, 15, A);

        Assert.Equal([A, B, C, D],
            Affected(null, order, order, Transfer(10, 11, 1, A, B), Transfer(11, 10, 1, A, C),
                Transfer(12, 10, 15, C, B), Transfer(13, 10, 20, B, D)));
    }

    //D186: დაპაუზებულ შექმნის ბრძანებას ჯგუფი ჯერ არ აქვს
    [Fact]
    public void AffectedGroupIds_PausedCreateOrder_IsNoGroup()
    {
        GroupOrderData paused = Order(20, EGroupOrderType.CreateGroup, Day(10, 1), A) with
        {
            GroupId = null, IsPaused = true
        };

        Assert.Empty(Affected(null, paused, paused, Transfer(25, 10, 1, A, B)));
    }
}
