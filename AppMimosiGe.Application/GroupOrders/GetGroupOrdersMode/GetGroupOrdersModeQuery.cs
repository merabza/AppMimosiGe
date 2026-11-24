using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrdersMode;

//პროგრამის რეჟიმი (D183): ბრძანებების გვერდსაც და ჯგუფის გვერდსაც სჭირდება
public sealed record GetGroupOrdersModeQuery : IQuery<GroupOrdersModeResponse>;
