using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.EndWork;

//პასუხი დასრულებული ჩანაწერია
public sealed record EndWorkCommand(WorkTimeFixRequest? Request) : ICommand<WorkHourResponse>;
