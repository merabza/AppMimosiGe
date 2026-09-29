using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.UpdateStudentContract;

public sealed record UpdateStudentContractCommand(int ScId, StudentContractRequest? Request) : ICommand;
