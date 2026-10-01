using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Balances.GetBalancesFormLookups;

public sealed record GetBalancesFormLookupsQuery : IQuery<BalancesFormLookupsResponse>;
