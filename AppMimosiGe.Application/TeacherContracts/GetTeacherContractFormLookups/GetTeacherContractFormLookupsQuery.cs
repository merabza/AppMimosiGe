using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContractFormLookups;

public sealed record GetTeacherContractFormLookupsQuery : IQuery<TeacherContractFormLookupsResponse>;
