using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContract;

public sealed record GetTeacherContractQuery(int Id) : IQuery<TeacherContractResponse>;
