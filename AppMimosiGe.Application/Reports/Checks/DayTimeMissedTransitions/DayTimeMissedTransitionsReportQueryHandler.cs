using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.DayTimeMissedTransitions;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DayTimeMissedTransitionsReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<DayTimeMissedTransitionsReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return TransitionReports.DayTimeMissedTransitions(snapshot);
    }
}
