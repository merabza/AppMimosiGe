using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.GetLessonsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetLessonsRowsDataQueryHandler(ILessonsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetLessonsRowsDataQuery, LessonsRowsDataResponse>
{
    public async Task<Result<LessonsRowsDataResponse>> Handle(GetLessonsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return LessonErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return LessonErrors.FilterSortRequestIsInvalid;
        }

        //გაკვეთილის დრო ბაზაში ადგილობრივია (Access-იდან)
        Result<LessonsListQuery> listQuery =
            LessonsListQueryFactory.Create(filterSortRequest, timeProvider.GetLocalNow().DateTime);
        if (listQuery.IsFailure)
        {
            return Result.Failure<LessonsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
