using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.GetPaymentsRowsData;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetPaymentsRowsDataQueryHandler(IPaymentsRepository repository)
    : IQueryHandler<GetPaymentsRowsDataQuery, PaymentsRowsDataResponse>
{
    public async Task<Result<PaymentsRowsDataResponse>> Handle(GetPaymentsRowsDataQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return PaymentErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return PaymentErrors.FilterSortRequestIsInvalid;
        }

        Result<PaymentsListQuery> listQuery = PaymentsListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<PaymentsRowsDataResponse>(listQuery.Error);
        }

        return await repository.GetRowsData(listQuery.Value, cancellationToken);
    }
}
