using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransactionStudentContracts;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetBankTransactionStudentContractsQueryHandler(IPaymentsRepository repository)
    : IQueryHandler<GetBankTransactionStudentContractsQuery, List<LookupItemResponse>>
{
    public async Task<Result<List<LookupItemResponse>>> Handle(GetBankTransactionStudentContractsQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetStudentContracts(request.AcademicYearId, cancellationToken);
    }
}
