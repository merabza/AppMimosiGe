using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.AcademicYears.GetAcademicYears;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetAcademicYearsQueryHandler(IAcademicYearsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetAcademicYearsQuery, AcademicYearsResponse>
{
    public async Task<Result<AcademicYearsResponse>> Handle(GetAcademicYearsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        return new AcademicYearsResponse(
        [
            .. academicYears.OrderBy(ay => ay.StartDate)
                .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
        ], CurrentAcademicYear.Find(academicYears, timeProvider.GetLocalNow().Date));
    }
}
