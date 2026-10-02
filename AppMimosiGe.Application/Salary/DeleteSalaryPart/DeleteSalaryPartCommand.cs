using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.DeleteSalaryPart;

public sealed record DeleteSalaryPartCommand(int SpId) : ICommand;
