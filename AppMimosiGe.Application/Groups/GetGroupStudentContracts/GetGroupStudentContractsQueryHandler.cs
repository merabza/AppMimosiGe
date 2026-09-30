using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.GetGroupStudentContracts;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupStudentContractsQueryHandler(IGroupsRepository repository)
    : IQueryHandler<GetGroupStudentContractsQuery, List<GroupStudentContractLookupResponse>>
{
    public async Task<Result<List<GroupStudentContractLookupResponse>>> Handle(GetGroupStudentContractsQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetStudentContracts(request.AcademicYearId, cancellationToken);
    }
}
