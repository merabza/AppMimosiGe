using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.GetWorkHour;

public sealed record GetWorkHourQuery(int WhId) : IQuery<WorkHourResponse>;
