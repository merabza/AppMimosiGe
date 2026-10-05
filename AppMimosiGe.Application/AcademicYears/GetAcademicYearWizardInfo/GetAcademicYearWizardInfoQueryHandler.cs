using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.AcademicYears.GetAcademicYearWizardInfo;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetAcademicYearWizardInfoQueryHandler(
    IAcademicYearsRepository repository,
    ILessonGeneratorRepository lessonGeneratorRepository,
    TimeProvider timeProvider) : IQueryHandler<GetAcademicYearWizardInfoQuery, AcademicYearWizardInfoResponse>
{
    public async Task<Result<AcademicYearWizardInfoResponse>> Handle(GetAcademicYearWizardInfoQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        DateTime? lastMonth = await lessonGeneratorRepository.GetLastOperationMonth(cancellationToken);
        return new AcademicYearWizardInfoResponse(await repository.GetAcademicYearsInfo(cancellationToken),
            CurrentAcademicYear.Find(academicYears, timeProvider.GetLocalNow().Date), lastMonth,
            lastMonth is { } month ? OperationMonthsCalendar.HorizonEnd(month) : null);
    }
}
