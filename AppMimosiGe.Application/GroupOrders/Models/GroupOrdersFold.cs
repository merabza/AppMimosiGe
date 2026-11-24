using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფების მდგომარეობა მათი ბრძანებებიდან (D185, GROUP_ORDERS.md §5 ნაბიჯი 3): ბრძანებები თარიღისა და Id-ის (შექმნის)
///     რიგით გაივლის, ჯგუფს საწყისი (0) ან შექმნის (1) ბრძანება ქმნის. გადაყვანა (5) ორივე ჯგუფს ერთად ცვლის.
///     დაპაუზებული ბრძანება ეფექტს არ იძლევა (D186). ბრძანება 7-ის თარიღზე ან მის შემდეგ ჯგუფის სხვა ბრძანება არ
///     ესადაგება (D190). პირველივე ბრძანებაზე, რომელიც მდგომარეობას აღარ ესადაგება, გატარება ჩერდება
/// </summary>
public static class GroupOrdersFold
{
    /// <summary>
    ///     orders: ჯგუფების ყველა ბრძანება, ნებისმიერი რიგით; ჯგუფი ბრძანების GroupId-ია (ახალ, ჯერ შეუნახავ ჯგუფს დროებითი
    ///     Id სჭირდება). გადაყვანის ორივე ჯგუფის ბრძანებები აქ უნდა იყოს: ჯგუფი, რომლის შექმნის ბრძანება აქ არ არის,
    ///     შექმნილად არ ითვლება
    /// </summary>
    public static GroupOrdersFoldResult Run(IEnumerable<GroupOrderData> orders)
    {
        Dictionary<int, GroupState> states = [];
        //ჯგუფის ბოლოს გატარებული ბრძანების დღე; გადაყვანა ორივე ჯგუფის ბრძანებაა
        Dictionary<int, DateTime> lastOrderDays = [];
        foreach (GroupOrderData order in orders.Where(o => !o.IsPaused).OrderBy(o => o.OrderDate).ThenBy(o => o.Id))
        {
            Error? error = order.OrderType == EGroupOrderType.TransferStudents
                ? Transfer(states, order)
                : ApplyToGroup(states, lastOrderDays, order);
            if (error is not null)
            {
                return new GroupOrdersFoldResult(new Dictionary<int, GroupState>(),
                    new GroupOrderNotFitting(order.Id, error));
            }

            foreach (int groupId in new[] { order.GroupId, order.TargetGroupId }.OfType<int>())
            {
                lastOrderDays[groupId] = order.OrderDate.Date;
            }
        }

        return new GroupOrdersFoldResult(states, null);
    }

    //ჯგუფს ახალი მდგომარეობა ეწერება; აბრუნებს მიზეზს, თუ ბრძანება არ ესადაგება
    private static Error? ApplyToGroup(Dictionary<int, GroupState> states, Dictionary<int, DateTime> lastOrderDays,
        GroupOrderData order)
    {
        int groupId = RequiredGroupId(order.GroupId, order);
        Result<GroupState> result = GroupOrderEffects.Apply(states.GetValueOrDefault(groupId), order);
        if (result.IsFailure)
        {
            return result.Error;
        }

        //D190: ბრძანება 7-ის დღეს ჯგუფს სხვა ბრძანება არ უნდა ჰქონდეს. გვიანდელს დახურული ჯგუფი არ იღებს
        //(GroupIsClosedOnOrderDate), იმავე დღის ადრე შექმნილი კი უკვე გატარდა
        if (order.OrderType == EGroupOrderType.CloseGroup && lastOrderDays.TryGetValue(groupId, out DateTime lastDay) &&
            lastDay >= order.OrderDate.Date)
        {
            return GroupOrderErrors.GroupHasLaterOrders;
        }

        states[groupId] = result.Value;
        return null;
    }

    private static Error? Transfer(Dictionary<int, GroupState> states, GroupOrderData order)
    {
        int sourceGroupId = RequiredGroupId(order.GroupId, order);
        int targetGroupId = RequiredGroupId(order.TargetGroupId, order);
        Result<(GroupState Source, GroupState Target)> result = GroupStudentOrderEffects.Transfer(
            states.GetValueOrDefault(sourceGroupId), states.GetValueOrDefault(targetGroupId), order);
        if (result.IsFailure)
        {
            return result.Error;
        }

        states[sourceGroupId] = result.Value.Source;
        states[targetGroupId] = result.Value.Target;
        return null;
    }

    private static int RequiredGroupId(int? groupId, GroupOrderData order)
    {
        return groupId ?? throw new InvalidOperationException($"Group order {order.Id} has no group");
    }
}
