using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.DeleteGroupOrder;

public sealed record DeleteGroupOrderCommand(int OrderId) : ICommand;
