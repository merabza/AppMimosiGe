using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ჯგუფი 1: საწყისი ბრძანება 1 (01.09, მოსწავლე 20), დამატებები 2 (მოსწავლე 21) და 3 (მოსწავლე 22) ერთ დღეს, 01.10,
//და მოსწავლე 21-ის გაყვანა 4 (01.11)
public sealed class GroupStatesBeforeOrderTests
{
    //ახალი ბრძანების Id გადათამაშებისას (GroupOrderSaveSteps.NewOrderId)
    private const int NewOrderId = int.MaxValue;

    private static readonly List<GroupOrderData> Orders =
    [
        InitialOrder(1, 1, InitialStudentLine(101, 20)),
        StudentsOrder(2, EGroupOrderType.AddStudents, Day(10, 1), 1, StudentLine(201, 21)),
        StudentsOrder(3, EGroupOrderType.AddStudents, Day(10, 1), 1, StudentLine(301, 22)),
        StudentsOrder(4, EGroupOrderType.RemoveStudents, Day(11, 1), 1, RemovalLine(401, 21))
    ];

    private static int[] StudentsOn(GroupOrdersFoldResult result, DateTime day)
    {
        Assert.Null(result.NotFitting);
        return
        [
            .. GroupOrderFormLookups.ActiveStudents(result.States[1], day).Select(s => s.StudentContractId).Order()
        ];
    }

    //ბრძანება თვითონ და იმავე დღის მასზე გვიან შექმნილი ბრძანება არ ითვლება, ადრე შექმნილი კი ითვლება
    [Fact]
    public void Run_ExistingOrder_CountsOnlyTheOrdersBeforeIt()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(10, 1), 3);

        Assert.Equal([20, 21], StudentsOn(result, Day(10, 1)));
    }

    [Fact]
    public void Run_FirstOrderOfTheDay_CountsNoOrderOfThatDay()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(10, 1), 2);

        Assert.Equal([20], StudentsOn(result, Day(10, 1)));
    }

    //ახალი ბრძანება თავისი დღის ბოლოა
    [Fact]
    public void Run_NewOrder_CountsEveryOrderOfItsDay()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(10, 1), NewOrderId);

        Assert.Equal([20, 21, 22], StudentsOn(result, Day(10, 1)));
    }

    //გვიანდელი თარიღის ბრძანება (4) არ ითვლება; დრო არ ითვლება
    [Fact]
    public void Run_LaterDateOrders_AreNotCounted()
    {
        GroupOrdersFoldResult result =
            GroupStatesBeforeOrder.Run(Orders, Day(10, 20).AddHours(15), NewOrderId);

        Assert.Equal([20, 21, 22], StudentsOn(result, Day(11, 2)));
    }

    //შეცვლისას ბრძანება ახალ თარიღზე გადადის: ძველ ადგილას ის აღარ ითვლება
    [Fact]
    public void Run_OrderMovedToALaterDate_IsNotCountedAtItsOldDate()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(10, 20), 2);

        Assert.Equal([20, 22], StudentsOn(result, Day(10, 20)));
    }

    //ბრძანების გარეშე გვიანდელი ბრძანება შეიძლება აღარ ესადაგებოდეს, როგორც შენახვისას (D185)
    [Fact]
    public void Run_WithoutTheOrderALaterOneNoLongerFits_ReturnsIt()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(11, 15), 2);

        Assert.NotNull(result.NotFitting);
        Assert.Equal(4, result.NotFitting.OrderId);
        Assert.Equal(GroupOrderErrors.StudentIsNotInGroup.Code, result.NotFitting.Error.Code);
    }

    //ჯგუფი, რომლის შექმნის ბრძანებაც ამ ადგილას ჯერ არ არის, შედეგში არ არის
    [Fact]
    public void Run_BeforeTheGroupStart_HasNoState()
    {
        GroupOrdersFoldResult result = GroupStatesBeforeOrder.Run(Orders, Day(8, 31), NewOrderId);

        Assert.Null(result.NotFitting);
        Assert.Empty(result.States);
    }
}
