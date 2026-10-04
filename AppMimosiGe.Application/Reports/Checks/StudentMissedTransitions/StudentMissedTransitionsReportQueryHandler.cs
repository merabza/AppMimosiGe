using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.StudentMissedTransitions;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentMissedTransitionsReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<StudentMissedTransitionsReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return TransitionReports.StudentMissedTransitions(snapshot);
    }
}
