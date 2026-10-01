using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.GetCrmCallsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetCrmCallsRowsDataQueryHandler(ICrmCallsRepository repository)
    : IQueryHandler<GetCrmCallsRowsDataQuery, CrmCallsRowsDataResponse>
{
    public async Task<Result<CrmCallsRowsDataResponse>> Handle(GetCrmCallsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return CrmCallErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return CrmCallErrors.FilterSortRequestIsInvalid;
        }

        Result<CrmCallsListQuery> listQuery = CrmCallsListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<CrmCallsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
