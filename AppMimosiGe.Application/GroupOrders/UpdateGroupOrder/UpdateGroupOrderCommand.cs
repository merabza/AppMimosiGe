using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.UpdateGroupOrder;

public sealed record UpdateGroupOrderCommand(int OrderId, GroupOrderRequest? Request) : ICommand;
