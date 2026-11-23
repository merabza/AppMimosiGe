using System;
using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     გადათამაშება (D185): ბრძანების შექმნის, შეცვლის ან წაშლისას შეხებული ჯგუფების მდგომარეობა თავიდან ითვლება მათი
///     ყველა ბრძანებიდან, თარიღისა და შექმნის რიგით (GroupOrdersFold)
/// </summary>
public static class GroupOrdersReplay
{
    /// <summary>
    ///     before: ბრძანება ცვლილებამდე (null: ახალი ბრძანება), after: ცვლილების შემდეგ (null: წაშლა). ახალ ბრძანებას ისეთი
    ///     Id უნდა ჰქონდეს, რომ ერთი დღის ბრძანებებში ბოლო იყოს. orders: ბრძანებები ცვლილებამდე; მათში უნდა იყოს ცვლილების
    ///     ჯგუფებთან გადაყვანებით დაკავშირებული ყველა ჯგუფის ყველა ბრძანება, მაგალითად ამ ჯგუფების სასწავლო წლის ყველა
    ///     ბრძანება (გადაყვანა ერთი წლის ჯგუფებს აკავშირებს). დაკავშირებულ ჯგუფებსაც გატარება სჭირდება, რადგან „სად“
    ///     ჯგუფის სტრიქონი „საიდან“-ის სტრიქონის დასრულებას იღებს (D205), შედეგში კი მხოლოდ შეხებული ჯგუფებია
    /// </summary>
    public static GroupOrdersReplayResult Run(GroupOrderData? before, GroupOrderData? after,
        IEnumerable<GroupOrderData> orders)
    {
        List<GroupOrderData> changed = [.. orders.Where(o => o.Id != before?.Id && o.Id != after?.Id)];
        if (after is not null)
        {
            changed.Add(after);
        }

        IReadOnlySet<int> affected = AffectedGroupIds(before, after, changed);
        HashSet<int> linked = LinkedGroupIds(affected, changed);
        GroupOrdersFoldResult fold =
            GroupOrdersFold.Run(changed.Where(o => o.GroupId is { } groupId && linked.Contains(groupId)));
        return new GroupOrdersReplayResult(affected,
            fold.States.Where(s => affected.Contains(s.Key)).ToDictionary(s => s.Key, s => s.Value),
            fold.NotFitting);
    }

    /// <summary>
    ///     შეხებული ჯგუფები (D185): ცვლილებამდელი და ცვლილების შემდგომი ბრძანების ჯგუფი (გადაყვანისას ორივე) და, ჯაჭვით,
    ///     ჯგუფები, რომლებსაც გვიანდელი გადაყვანები უკავშირებს. ჯგუფის მდგომარეობა შეიძლება შეიცვალოს ცვლილების ადგილიდან
    ///     (თარიღი, Id), ჯაჭვით დაკავშირებულისა კი დამაკავშირებელი გადაყვანიდან, ამიტომ მისგან ჯაჭვი მხოლოდ ამ გადაყვანაზე
    ///     გვიანდელი გადაყვანებით გრძელდება. orders: ბრძანებები ცვლილების შემდეგ
    /// </summary>
    public static IReadOnlySet<int> AffectedGroupIds(GroupOrderData? before, GroupOrderData? after,
        IEnumerable<GroupOrderData> orders)
    {
        List<GroupOrderData> transfers = [.. orders.Where(IsTransfer)];
        Dictionary<int, (DateTime Date, int Id)> changedFrom = [];
        Queue<int> toVisit = new();
        foreach (GroupOrderData order in new[] { before, after }.OfType<GroupOrderData>())
        {
            foreach (int groupId in GroupIds(order))
            {
                Affect(changedFrom, toVisit, groupId, Position(order));
            }
        }

        while (toVisit.TryDequeue(out int groupId))
        {
            (DateTime Date, int Id) from = changedFrom[groupId];
            foreach (GroupOrderData transfer in transfers.Where(t =>
                         Involves(t, groupId) && Position(t).CompareTo(from) > 0))
            {
                foreach (int linkedGroupId in GroupIds(transfer))
                {
                    Affect(changedFrom, toVisit, linkedGroupId, Position(transfer));
                }
            }
        }

        return changedFrom.Keys.ToHashSet();
    }

    //groupIds და ყველა ჯგუფი, რომელსაც მათთან გადაყვანები აკავშირებს, ნებისმიერი თარიღით (D205)
    private static HashSet<int> LinkedGroupIds(IEnumerable<int> groupIds, List<GroupOrderData> orders)
    {
        List<GroupOrderData> transfers = orders.FindAll(IsTransfer);
        HashSet<int> linked = [.. groupIds];
        Queue<int> toVisit = new(linked);
        while (toVisit.TryDequeue(out int groupId))
        {
            foreach (int linkedGroupId in transfers.Where(t => Involves(t, groupId)).SelectMany(GroupIds))
            {
                if (linked.Add(linkedGroupId))
                {
                    toVisit.Enqueue(linkedGroupId);
                }
            }
        }

        return linked;
    }

    //ჯგუფი შეხებულია position-იდან; თუ ის უკვე შეხებულია უფრო გვიანდელი ადგილიდან, ჯაჭვი მისგან თავიდან მოწმდება
    private static void Affect(Dictionary<int, (DateTime Date, int Id)> changedFrom, Queue<int> toVisit, int groupId,
        (DateTime Date, int Id) position)
    {
        if (changedFrom.TryGetValue(groupId, out (DateTime Date, int Id) current) && current.CompareTo(position) <= 0)
        {
            return;
        }

        changedFrom[groupId] = position;
        toVisit.Enqueue(groupId);
    }

    //ბრძანების ადგილი გატარების რიგში
    private static (DateTime Date, int Id) Position(GroupOrderData order)
    {
        return (order.OrderDate, order.Id);
    }

    private static bool IsTransfer(GroupOrderData order)
    {
        return order is { OrderType: EGroupOrderType.TransferStudents, IsPaused: false };
    }

    private static bool Involves(GroupOrderData order, int groupId)
    {
        return order.GroupId == groupId || order.TargetGroupId == groupId;
    }

    private static IEnumerable<int> GroupIds(GroupOrderData order)
    {
        return new[] { order.GroupId, order.TargetGroupId }.OfType<int>();
    }
}
