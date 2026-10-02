using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Salary.DeleteSalaryHeader;

public sealed record DeleteSalaryHeaderCommand(int ShId) : ICommand;
