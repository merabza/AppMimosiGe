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

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStudentContractFormLookupsQueryHandler(
    IStudentContractsRepository repository,
    TimeProvider timeProvider) : IQueryHandler<GetStudentContractFormLookupsQuery, StudentContractFormLookupsResponse>
{
    public async Task<Result<StudentContractFormLookupsResponse>> Handle(GetStudentContractFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        DateTime today = timeProvider.GetLocalNow().Date;

        //Access-ის ფორმის მსგავსად წლები სახელით ლაგდება
        return new StudentContractFormLookupsResponse(CurrentAcademicYear.Find(academicYears, today),
            [
                .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                    .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
            ], await repository.GetStudentStatuses(cancellationToken), await repository.GetCourses(cancellationToken),
            await repository.GetGroupSizes(cancellationToken));
    }
}
