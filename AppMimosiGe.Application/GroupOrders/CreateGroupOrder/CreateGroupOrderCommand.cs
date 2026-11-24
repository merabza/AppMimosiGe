using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.CreateGroupOrder;

//პასუხი ახალი ბრძანების იდენტიფიკატორია
public sealed record CreateGroupOrderCommand(GroupOrderRequest? Request) : ICommand<int>;
