using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;

public sealed record GetAcademicYearWizardInfoQuery : IQuery<AcademicYearWizardInfoResponse>;
