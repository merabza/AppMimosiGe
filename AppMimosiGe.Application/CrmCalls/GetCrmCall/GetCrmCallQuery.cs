using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCall;

public sealed record GetCrmCallQuery(int CrmCallId) : IQuery<CrmCallResponse>;
