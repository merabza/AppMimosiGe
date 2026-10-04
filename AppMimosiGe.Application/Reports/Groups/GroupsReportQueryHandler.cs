using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Groups;

/// <summary>
///     ჯგუფების რეპორტების საერთო handler: თარიღის აქტიური ჯგუფები და მათი სტრიქონები ბაზიდან, შემდეგ რეპორტის სუფთა
///     ლოგიკა (Groups/Models)
/// </summary>
public abstract class GroupsReportQueryHandler<TQuery>(IReportsRepository repository)
    : IQueryHandler<TQuery, ReportTable> where TQuery : GroupsReportQuery
{
    public async Task<Result<ReportTable>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        GroupsSnapshot snapshot = await repository.GetGroups(query.Date, cancellationToken);
        return Build(query, snapshot);
    }

    protected abstract ReportTable Build(TQuery query, GroupsSnapshot snapshot);
}
