using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.UpdateWorkHour;

public sealed record UpdateWorkHourCommand(int WhId, WorkHourRequest? Request) : ICommand;
