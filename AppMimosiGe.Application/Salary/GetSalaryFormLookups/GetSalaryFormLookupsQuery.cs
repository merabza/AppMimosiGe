using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.GetSalaryFormLookups;

public sealed record GetSalaryFormLookupsQuery : IQuery<SalaryFormLookupsResponse>;
