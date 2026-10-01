using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCall;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetCrmCallQueryHandler(ICrmCallsRepository repository)
    : IQueryHandler<GetCrmCallQuery, CrmCallResponse>
{
    public async Task<Result<CrmCallResponse>> Handle(GetCrmCallQuery request, CancellationToken cancellationToken)
    {
        CrmCallResponse? crmCall = await repository.GetOne(request.CrmCallId, cancellationToken);
        return crmCall is null ? CrmCallErrors.CrmCallNotFound : crmCall;
    }
}
