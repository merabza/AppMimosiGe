using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.TeacherSchemeMismatches;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherSchemeMismatchesReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<TeacherSchemeMismatchesReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return MismatchReports.TeacherSchemeMismatches(snapshot);
    }
}
