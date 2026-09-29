using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;

public sealed record GetStudentContractFormLookupsQuery : IQuery<StudentContractFormLookupsResponse>;
