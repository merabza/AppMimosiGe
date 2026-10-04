using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.TeacherSameStartEndDate;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherSameStartEndDateReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<TeacherSameStartEndDateReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return SameStartEndDateReports.TeacherSameStartEndDate(snapshot);
    }
}
