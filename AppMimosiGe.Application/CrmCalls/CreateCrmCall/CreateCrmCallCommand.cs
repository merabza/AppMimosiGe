using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.CreateCrmCall;

//პასუხი ახალი ზარის იდენტიფიკატორია
public sealed record CreateCrmCallCommand(CrmCallRequest? Request) : ICommand<int>;
