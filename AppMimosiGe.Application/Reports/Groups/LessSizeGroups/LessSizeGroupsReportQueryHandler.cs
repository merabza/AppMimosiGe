using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Groups.LessSizeGroups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class LessSizeGroupsReportQueryHandler(IReportsRepository repository)
    : GroupsReportQueryHandler<LessSizeGroupsReportQuery>(repository)
{
    protected override ReportTable Build(LessSizeGroupsReportQuery query, GroupsSnapshot snapshot)
    {
        return GroupFillReports.LessSizeGroups(snapshot);
    }
}
