using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCallFormLookups;

public sealed record GetCrmCallFormLookupsQuery : IQuery<CrmCallFormLookupsResponse>;
