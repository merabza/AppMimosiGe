using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;

public sealed record UpdateTeacherContractCommand(int Id, TeacherContractRequest? Request) : ICommand;
