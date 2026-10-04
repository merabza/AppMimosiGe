using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.StudentFeeMismatches;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentFeeMismatchesReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<StudentFeeMismatchesReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return MismatchReports.StudentFeeMismatches(snapshot);
    }
}
