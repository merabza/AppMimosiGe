using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.GetWorkHoursRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetWorkHoursRowsDataQueryHandler(IWorkHoursRepository repository)
    : IQueryHandler<GetWorkHoursRowsDataQuery, WorkHoursRowsDataResponse>
{
    public async Task<Result<WorkHoursRowsDataResponse>> Handle(GetWorkHoursRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return WorkHourErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return WorkHourErrors.FilterSortRequestIsInvalid;
        }

        Result<WorkHoursListQuery> listQuery = WorkHoursListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<WorkHoursRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
