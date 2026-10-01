using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Crud.Models;
using BackendCarcass.Application.FilterSort.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.GetStatement;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetStatementQueryHandler(IBalancesRepository repository)
    : IQueryHandler<GetStatementQuery, StatementRowsDataResponse>
{
    //running total-ს კონტრაქტის (ან ყველა კონტრაქტის) ყველა ოპერაცია სჭირდება, ამიტომ ისინი ყოველთვის იტვირთება და
    //ფილტრი და გვერდი მეხსიერებაში ითვლება (Dev-ში ყველა კონტრაქტზე ~16.6 ათასი ოპერაციაა)
    public async Task<Result<StatementRowsDataResponse>> Handle(GetStatementQuery request,
        CancellationToken cancellationToken)
    {
        FilterSortRequest? filterSortRequest;
        try
        {
            filterSortRequest = FilterSortRequestFactory.Create(request.FilterSortRequest);
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return BalanceErrors.FilterSortRequestIsInvalid;
        }

        if (filterSortRequest is null)
        {
            return BalanceErrors.FilterSortRequestIsInvalid;
        }

        Result<StatementListQuery> listQuery = StatementListQueryFactory.Create(filterSortRequest);
        if (listQuery.IsFailure)
        {
            return Result.Failure<StatementRowsDataResponse>(listQuery.Error);
        }

        StatementListQuery query = listQuery.Value;
        int[]? scIds = query.StudentContractId is { } studentContractId ? [studentContractId] : null;
        List<BalanceOperation> operations = BalanceOperations.Build(
            await repository.GetCharges(scIds, cancellationToken),
            await repository.GetPayments(scIds, cancellationToken));
        Statement statement = StatementCalculator.Build(operations, query.DateFrom, query.DateTo);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int count = statement.Rows.Count;
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        List<StatementRow> page = [.. statement.Rows.Skip(offset).Take(query.RowsCount)];
        Dictionary<int, string> names = await repository.GetStudentContractNames(
            [.. page.Select(r => r.Operation.StudentContractId).Distinct()], cancellationToken);

        return new StatementRowsDataResponse(count, offset, statement.StartBalance, statement.EndBalance, [
            .. page.Select(r => new StatementRowResponse(r.Operation.IsPayment, r.Operation.Id,
                r.Operation.StudentContractId, names.GetValueOrDefault(r.Operation.StudentContractId, string.Empty),
                r.Operation.OperationDate, r.Operation.Document, BalanceOperations.RoundMoney(r.Operation.Amount),
                r.RunningTotal))
        ]);
    }
}
