using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.GetSalaryHeaders;

public sealed record GetSalaryHeadersQuery : IQuery<List<SalaryHeaderRowResponse>>;
