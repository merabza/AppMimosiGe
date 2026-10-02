using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.UpdateSalaryPart;

public sealed record UpdateSalaryPartCommand(int SpId, SalaryPartRequest? Request) : ICommand;
