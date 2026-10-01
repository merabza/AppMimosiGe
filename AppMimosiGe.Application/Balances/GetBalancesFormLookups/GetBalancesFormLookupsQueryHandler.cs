using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.GetBalancesFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetBalancesFormLookupsQueryHandler(
    IStudentContractsRepository studentContractsRepository,
    TimeProvider timeProvider) : IQueryHandler<GetBalancesFormLookupsQuery, BalancesFormLookupsResponse>
{
    //სასწავლო წლები და მიმდინარე წელი იგივეა (და იგივენაირად ლაგდება), რაც კონტრაქტებისა და გადახდების ფორმებში
    public async Task<Result<BalancesFormLookupsResponse>> Handle(GetBalancesFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await studentContractsRepository.GetAcademicYears(cancellationToken);
        DateTime today = timeProvider.GetLocalNow().Date;

        return new BalancesFormLookupsResponse(CurrentAcademicYear.Find(academicYears, today), [
            .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
        ]);
    }
}
