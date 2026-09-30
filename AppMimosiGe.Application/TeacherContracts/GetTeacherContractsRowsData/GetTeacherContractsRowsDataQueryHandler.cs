using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.GetTeacherContractsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetTeacherContractsRowsDataQueryHandler(
    ITeacherContractsRepository repository,
    TimeProvider timeProvider) : IQueryHandler<GetTeacherContractsRowsDataQuery, TeacherContractsRowsDataResponse>
{
    public async Task<Result<TeacherContractsRowsDataResponse>> Handle(GetTeacherContractsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return TeacherContractErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return TeacherContractErrors.FilterSortRequestIsInvalid;
        }

        Result<TeacherContractsListQuery> listQuery =
            TeacherContractsListQueryFactory.Create(filterSortRequest, timeProvider.GetLocalNow().Date);
        if (listQuery.IsFailure)
        {
            return Result.Failure<TeacherContractsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
