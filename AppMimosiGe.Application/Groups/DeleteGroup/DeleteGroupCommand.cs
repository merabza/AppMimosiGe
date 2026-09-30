using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.DeleteGroup;

public sealed record DeleteGroupCommand(int GrpId) : ICommand;
