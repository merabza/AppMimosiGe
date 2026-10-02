using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.AutoGenerateWorkHours;

public sealed record AutoGenerateWorkHoursCommand(WorkHoursAutoGenerateRequest? Request)
    : ICommand<WorkHoursAutoGenerateResponse>;
