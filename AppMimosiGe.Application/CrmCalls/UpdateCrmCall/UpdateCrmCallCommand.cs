using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.UpdateCrmCall;

public sealed record UpdateCrmCallCommand(int CrmCallId, CrmCallRequest? Request) : ICommand;
