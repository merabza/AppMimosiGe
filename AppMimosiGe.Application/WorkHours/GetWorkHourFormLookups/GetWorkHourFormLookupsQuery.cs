using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.GetWorkHourFormLookups;

public sealed record GetWorkHourFormLookupsQuery : IQuery<WorkHourFormLookupsResponse>;
