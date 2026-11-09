using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransactions;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetBankTransactionsQueryHandler(IBankTransactionsRepository repository)
    : IQueryHandler<GetBankTransactionsQuery, List<BankTransactionRowResponse>>
{
    //query-ს ვალიდატორი არ ეშვება (ValidationDecorator მხოლოდ command-ებისაა), ამიტომ პარამეტრები აქ მოწმდება
    public async Task<Result<List<BankTransactionRowResponse>>> Handle(GetBankTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!TryParseFilter(request.Filter, out EBankTransactionsFilter filter))
        {
            return BankTransactionErrors.ListFilterIsInvalid;
        }

        if (request.DateFrom > request.DateTo)
        {
            return BankTransactionErrors.PeriodIsInvalid;
        }

        return await repository.GetRows(request.DateFrom?.Date, request.DateTo?.Date, filter, cancellationToken);
    }

    //ცარიელი: ფილტრის გარეშე. სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
    private static bool TryParseFilter(string? value, out EBankTransactionsFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            filter = EBankTransactionsFilter.None;
            return true;
        }

        return Enum.TryParse(value, true, out filter) && Enum.IsDefined(filter) && !int.TryParse(value, out _);
    }
}
