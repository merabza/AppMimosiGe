using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCallStudentContracts;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetCrmCallStudentContractsQueryHandler(ICrmCallsRepository repository)
    : IQueryHandler<GetCrmCallStudentContractsQuery, List<LookupItemResponse>>
{
    public async Task<Result<List<LookupItemResponse>>> Handle(GetCrmCallStudentContractsQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetStudentContracts(request.AcademicYearId, cancellationToken);
    }
}
