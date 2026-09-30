using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;

public sealed record DeleteTeacherContractCommand(int Id) : ICommand;
