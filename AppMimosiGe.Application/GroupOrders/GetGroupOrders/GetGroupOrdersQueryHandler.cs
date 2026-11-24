using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.GetGroupOrders;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupOrdersQueryHandler(IGroupOrdersRepository repository)
    : IQueryHandler<GetGroupOrdersQuery, GroupOrdersRowsDataResponse>
{
    public async Task<Result<GroupOrdersRowsDataResponse>> Handle(GetGroupOrdersQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return GroupOrderErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return GroupOrderErrors.FilterSortRequestIsInvalid;
        }

        Result<GroupOrdersListQuery> listQuery = GroupOrdersListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<GroupOrdersRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
