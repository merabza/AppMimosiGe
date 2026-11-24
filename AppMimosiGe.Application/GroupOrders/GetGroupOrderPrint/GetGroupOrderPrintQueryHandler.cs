using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrderPrint;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrderPrintQueryHandler(IGroupOrdersRepository repository)
    : IQueryHandler<GetGroupOrderPrintQuery, GroupOrderPrintResponse>
{
    //ორგანიზაცია და ხელმომწერი ბრძანებიდან მოდის, არა კონფიგურაციიდან (D193)
    public async Task<Result<GroupOrderPrintResponse>> Handle(GetGroupOrderPrintQuery request,
        CancellationToken cancellationToken)
    {
        GroupOrderPrintSource? source = await repository.GetPrintSource(request.OrderId, cancellationToken);
        if (source is null)
        {
            return GroupOrderErrors.GroupOrderNotFound;
        }

        GroupOrderData order = source.Order;
        if (order.OrderType == EGroupOrderType.Initial)
        {
            return GroupOrderErrors.InitialOrderIsNotPrinted;
        }

        if (order.IsPaused)
        {
            return GroupOrderErrors.PausedOrderIsNotPrinted;
        }

        GroupState? previous = GroupOrderPrint.ShowsPreviousState(order.OrderType)
            ? await PreviousState(order, cancellationToken)
            : null;
        GroupOrderPrintNames names = await repository.GetPrintNames(
            GroupOrderPrint.TeacherContractIds(order, previous), GroupOrderPrint.StudentContractIds(order),
            cancellationToken);
        return GroupOrderPrint.Build(source, previous, names);
    }

    //ჯგუფის მდგომარეობა ბრძანებამდე. ეს შენახული ბრძანებების რიგის დასაწყისია, რომელიც ყოველ შენახვაზე მთლიანად
    //მოწმდება, ამიტომ ის ყოველთვის ესადაგება; 2–10 ბრძანებას ყოველთვის აქვს ჯგუფი, რომელიც მასზე ადრეა შექმნილი
    private async Task<GroupState> PreviousState(GroupOrderData order, CancellationToken cancellationToken)
    {
        int groupId = order.GroupId ??
                      throw new InvalidOperationException($"Group order {order.Id} has no group");
        List<GroupOrderData> orders = await repository.GetLinkedOrders([groupId], cancellationToken);
        GroupOrdersFoldResult fold = GroupStatesBeforeOrder.Run(orders, order.OrderDate, order.Id);
        return fold.States.TryGetValue(groupId, out GroupState? state)
            ? state
            : throw new InvalidOperationException($"The group of group order {order.Id} has no state before it");
    }
}
