using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrdersMode;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrdersModeQueryHandler(IGroupOrdersRepository repository)
    : IQueryHandler<GetGroupOrdersModeQuery, GroupOrdersModeResponse>
{
    public async Task<Result<GroupOrdersModeResponse>> Handle(GetGroupOrdersModeQuery request,
        CancellationToken cancellationToken)
    {
        return await repository.GetMode(cancellationToken);
    }
}
