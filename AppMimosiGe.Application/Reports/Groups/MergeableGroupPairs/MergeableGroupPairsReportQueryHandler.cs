using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Groups.MergeableGroupPairs;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class MergeableGroupPairsReportQueryHandler(IReportsRepository repository)
    : GroupsReportQueryHandler<MergeableGroupPairsReportQuery>(repository)
{
    protected override ReportTable Build(MergeableGroupPairsReportQuery query, GroupsSnapshot snapshot)
    {
        return GroupFillReports.Optimization(snapshot);
    }
}
