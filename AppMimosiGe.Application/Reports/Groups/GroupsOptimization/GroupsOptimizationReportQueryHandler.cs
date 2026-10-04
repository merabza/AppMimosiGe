using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Groups.GroupsOptimization;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GroupsOptimizationReportQueryHandler(IReportsRepository repository)
    : GroupsReportQueryHandler<GroupsOptimizationReportQuery>(repository)
{
    protected override ReportTable Build(GroupsOptimizationReportQuery query, GroupsSnapshot snapshot)
    {
        return GroupFillReports.GroupsOptimization(snapshot);
    }
}
