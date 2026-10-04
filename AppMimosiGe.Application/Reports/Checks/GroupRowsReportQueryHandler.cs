using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Checks;

/// <summary>
///     შემოწმების რეპორტების საერთო handler: ჯგუფების ყველა სტრიქონი ბაზიდან, შემდეგ რეპორტის სუფთა ლოგიკა
///     (Checks/Models)
/// </summary>
public abstract class GroupRowsReportQueryHandler<TQuery>(IReportsRepository repository)
    : IQueryHandler<TQuery, ReportTable> where TQuery : GroupRowsReportQuery
{
    public async Task<Result<ReportTable>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        GroupRowsSnapshot snapshot = await repository.GetGroupRows(cancellationToken);
        return Build(snapshot);
    }

    protected abstract ReportTable Build(GroupRowsSnapshot snapshot);
}
