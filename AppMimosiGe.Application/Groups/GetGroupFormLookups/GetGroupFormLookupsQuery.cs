using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Groups.GetGroupFormLookups;

public sealed record GetGroupFormLookupsQuery : IQuery<GroupFormLookupsResponse>;
