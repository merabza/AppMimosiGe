using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.GetStudentContractsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStudentContractsRowsDataQueryHandler(IStudentContractsRepository repository)
    : IQueryHandler<GetStudentContractsRowsDataQuery, StudentContractsRowsDataResponse>
{
    public async Task<Result<StudentContractsRowsDataResponse>> Handle(GetStudentContractsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return StudentContractErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return StudentContractErrors.FilterSortRequestIsInvalid;
        }

        Result<StudentContractsListQuery> listQuery = StudentContractsListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<StudentContractsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
