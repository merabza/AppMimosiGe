using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.GetSalaryHeader;

public sealed record GetSalaryHeaderQuery(int ShId) : IQuery<SalaryHeaderResponse>;
