using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.GetStatementStudentContracts;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStatementStudentContractsQueryHandler(IPaymentsRepository paymentsRepository)
    : IQueryHandler<GetStatementStudentContractsQuery, List<LookupItemResponse>>
{
    //იგივე სია, რაც გადახდების ფილტრსა და ფორმაში (ამ გვერდის უფლებით)
    public async Task<Result<List<LookupItemResponse>>> Handle(GetStatementStudentContractsQuery request,
        CancellationToken cancellationToken)
    {
        return await paymentsRepository.GetStudentContracts(request.AcademicYearId, cancellationToken);
    }
}
