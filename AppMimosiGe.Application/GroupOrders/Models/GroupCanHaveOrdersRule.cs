using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     რომელ ჯგუფს შეიძლება ბრძანება (D202). ბრძანებები 2–10 ეხება მხოლოდ ჯგუფს, რომელსაც აქვს საწყისი (0) ან
///     გააქტიურებული შექმნის (1) ბრძანება, გადაყვანისას სამიზნე ჯგუფსაც. საწყისი და შექმნის ბრძანება კი ახალ ჯგუფს
///     ქმნის: მის ჯგუფს სხვა ასეთი ბრძანება არ უნდა ჰქონდეს. დაპაუზებული შექმნის ბრძანება ჯგუფს ჯერ არ ქმნის (D186)
/// </summary>
public static class GroupCanHaveOrdersRule
{
    /// <summary>
    ///     orders: ბრძანებები, რომლებშიც order-ის ჯგუფების საწყისი და შექმნის ბრძანებებია, მაგალითად მათი სასწავლო წლის
    ///     ყველა ბრძანება; order შეიძლება მათში იყოს. აბრუნებს შეცდომას, ან null-ს
    /// </summary>
    public static Error? Check(GroupOrderData order, IEnumerable<GroupOrderData> orders)
    {
        HashSet<int> createdGroupIds =
            [.. orders.Where(o => o.Id != order.Id && IsCreating(o)).Select(o => o.GroupId).OfType<int>()];
        if (order.OrderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup)
        {
            return order.GroupId is { } groupId && createdGroupIds.Contains(groupId)
                ? GroupOrderErrors.CreateOrderCannotHaveGroup
                : null;
        }

        if (order.GroupId is not { } orderGroupId || !createdGroupIds.Contains(orderGroupId))
        {
            return GroupOrderErrors.GroupCannotHaveOrders;
        }

        return order.OrderType == EGroupOrderType.TransferStudents &&
               (order.TargetGroupId is not { } targetGroupId || !createdGroupIds.Contains(targetGroupId))
            ? GroupOrderErrors.TargetGroupCannotHaveOrders
            : null;
    }

    private static bool IsCreating(GroupOrderData order)
    {
        return order is { IsPaused: false, OrderType: EGroupOrderType.Initial or EGroupOrderType.CreateGroup };
    }
}
