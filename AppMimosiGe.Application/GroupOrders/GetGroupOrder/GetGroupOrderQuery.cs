using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrder;

public sealed record GetGroupOrderQuery(int OrderId) : IQuery<GroupOrderResponse>;
