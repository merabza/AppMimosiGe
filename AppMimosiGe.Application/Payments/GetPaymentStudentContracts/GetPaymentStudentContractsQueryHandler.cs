using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.GetPaymentStudentContracts;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetPaymentStudentContractsQueryHandler(IPaymentsRepository repository)
    : IQueryHandler<GetPaymentStudentContractsQuery, List<LookupItemResponse>>
{
    public async Task<Result<List<LookupItemResponse>>> Handle(GetPaymentStudentContractsQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetStudentContracts(request.AcademicYearId, cancellationToken);
    }
}
