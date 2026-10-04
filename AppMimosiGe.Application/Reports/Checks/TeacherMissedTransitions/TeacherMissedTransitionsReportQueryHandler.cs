using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.TeacherMissedTransitions;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherMissedTransitionsReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<TeacherMissedTransitionsReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return TransitionReports.TeacherMissedTransitions(snapshot);
    }
}
