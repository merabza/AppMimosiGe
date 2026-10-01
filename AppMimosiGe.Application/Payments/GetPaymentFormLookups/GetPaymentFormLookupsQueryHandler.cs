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

namespace AppMimosiGe.Application.Payments.GetPaymentFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetPaymentFormLookupsQueryHandler(
    IPaymentsRepository repository,
    IStudentContractsRepository studentContractsRepository,
    TimeProvider timeProvider) : IQueryHandler<GetPaymentFormLookupsQuery, PaymentFormLookupsResponse>
{
    //სასწავლო წლები და მიმდინარე წელი იგივეა (და იგივენაირად ლაგდება), რაც კონტრაქტების ფორმაში
    public async Task<Result<PaymentFormLookupsResponse>> Handle(GetPaymentFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await studentContractsRepository.GetAcademicYears(cancellationToken);
        DateTime today = timeProvider.GetLocalNow().Date;

        return new PaymentFormLookupsResponse(CurrentAcademicYear.Find(academicYears, today), [
            .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
        ], await repository.GetBankAccounts(cancellationToken));
    }
}
