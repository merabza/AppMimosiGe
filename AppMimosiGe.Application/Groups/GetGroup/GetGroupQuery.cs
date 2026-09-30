using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.GetGroup;

public sealed record GetGroupQuery(int GrpId) : IQuery<GroupResponse>;
