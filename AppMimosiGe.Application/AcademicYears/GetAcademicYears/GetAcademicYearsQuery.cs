using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.AcademicYears.GetAcademicYears;

public sealed record GetAcademicYearsQuery : IQuery<AcademicYearsResponse>;
