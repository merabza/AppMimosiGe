using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.CountSalary;

public sealed record CountSalaryCommand(int ShId) : ICommand<SalaryCountResponse>;
