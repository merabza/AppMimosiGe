using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrderPrint;

public sealed record GetGroupOrderPrintQuery(int OrderId) : IQuery<GroupOrderPrintResponse>;
