using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.UpdateSalaryHeader;

public sealed record UpdateSalaryHeaderCommand(int ShId, SalaryHeaderRequest? Request) : ICommand;
