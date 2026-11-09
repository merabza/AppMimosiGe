using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Finance.UnallocatedBankTransactions;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UnallocatedBankTransactionsReportQueryHandler(IBankTransactionsRepository repository)
    : IQueryHandler<UnallocatedBankTransactionsReportQuery, ReportTable>
{
    public async Task<Result<ReportTable>> Handle(UnallocatedBankTransactionsReportQuery query,
        CancellationToken cancellationToken)
    {
        List<BankTransactionRowResponse> rows =
            await repository.GetRows(null, null, EBankTransactionsFilter.Unallocated, cancellationToken);
        return BankTransactionReports.Unallocated(rows);
    }
}
