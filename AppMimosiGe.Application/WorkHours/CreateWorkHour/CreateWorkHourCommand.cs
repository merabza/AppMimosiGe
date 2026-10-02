using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.CreateWorkHour;

//პასუხი ახალი ჩანაწერის იდენტიფიკატორია
public sealed record CreateWorkHourCommand(WorkHourRequest? Request) : ICommand<int>;
