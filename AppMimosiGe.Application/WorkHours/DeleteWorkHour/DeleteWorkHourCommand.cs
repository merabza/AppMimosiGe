using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.WorkHours.DeleteWorkHour;

public sealed record DeleteWorkHourCommand(int WhId) : ICommand;
