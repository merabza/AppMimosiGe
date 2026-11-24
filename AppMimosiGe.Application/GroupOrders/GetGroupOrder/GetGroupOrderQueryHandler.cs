using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrder;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrderQueryHandler(IGroupOrdersRepository repository)
    : IQueryHandler<GetGroupOrderQuery, GroupOrderResponse>
{
    public async Task<Result<GroupOrderResponse>> Handle(GetGroupOrderQuery request,
        CancellationToken cancellationToken)
    {
        GroupOrderResponse? order = await repository.GetOne(request.OrderId, cancellationToken);
        return order is null ? GroupOrderErrors.GroupOrderNotFound : order;
    }
}
