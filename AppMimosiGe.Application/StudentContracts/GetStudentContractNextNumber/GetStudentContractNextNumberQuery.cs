using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractNextNumber;

public sealed record GetStudentContractNextNumberQuery(int AcademicYearId) : IQuery<StudentContractNextNumberResponse>;
