using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.UpdateGroup;

public sealed record UpdateGroupCommand(int GrpId, GroupRequest? Request) : ICommand;
