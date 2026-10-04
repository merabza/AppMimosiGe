using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Finance.BlackList;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class BlackListReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<BlackListReportQuery, ReportTable>
{
    public async Task<Result<ReportTable>> Handle(BlackListReportQuery query, CancellationToken cancellationToken)
    {
        BlackListData data = await repository.GetDesperateDebts(cancellationToken);
        return FinanceReports.BlackList(data);
    }
}
