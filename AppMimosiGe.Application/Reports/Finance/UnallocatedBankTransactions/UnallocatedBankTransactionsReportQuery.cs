using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Finance.UnallocatedBankTransactions;

//r40UnallocatedBankTransactions: ბოლომდე გაუნაწილებელი ჩარიცხვები (D164)
public sealed record UnallocatedBankTransactionsReportQuery : IQuery<ReportTable>;
