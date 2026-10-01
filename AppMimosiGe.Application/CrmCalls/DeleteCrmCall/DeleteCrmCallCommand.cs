using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.CrmCalls.DeleteCrmCall;

public sealed record DeleteCrmCallCommand(int CrmCallId) : ICommand;
