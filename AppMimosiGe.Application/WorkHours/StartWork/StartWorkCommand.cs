using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.StartWork;

//პასუხი შექმნილი ჩანაწერია
public sealed record StartWorkCommand(WorkTimeFixRequest? Request) : ICommand<WorkHourResponse>;
