using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Groups.GroupSizesAnalysis;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GroupSizesAnalysisReportQueryHandler(IReportsRepository repository)
    : GroupsReportQueryHandler<GroupSizesAnalysisReportQuery>(repository)
{
    protected override ReportTable Build(GroupSizesAnalysisReportQuery query, GroupsSnapshot snapshot)
    {
        return GroupFillReports.GroupSizesAnalysis(snapshot);
    }
}
