using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContract;

public sealed record GetStudentContractQuery(int ScId) : IQuery<StudentContractResponse>;
