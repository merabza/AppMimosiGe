using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.DeleteStudentContract;

public sealed record DeleteStudentContractCommand(int ScId) : ICommand;
