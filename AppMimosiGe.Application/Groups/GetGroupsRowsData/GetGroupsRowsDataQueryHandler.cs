using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.GetGroupsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupsRowsDataQueryHandler(IGroupsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetGroupsRowsDataQuery, GroupsRowsDataResponse>
{
    public async Task<Result<GroupsRowsDataResponse>> Handle(GetGroupsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return GroupErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return GroupErrors.FilterSortRequestIsInvalid;
        }

        Result<GroupsListQuery> listQuery =
            GroupsListQueryFactory.Create(filterSortRequest, timeProvider.GetLocalNow().Date);
        if (listQuery.IsFailure)
        {
            return Result.Failure<GroupsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
