using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.SearchHumans;

public sealed record SearchHumansQuery(string? Search) : IQuery<List<LookupItemResponse>>;
