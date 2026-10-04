using AppMimosiGe.Application.Reports.Checks.Models;
using AppMimosiGe.Application.Reports.Models;

namespace AppMimosiGe.Application.Reports.Checks.DayTimeSameStartEndDate;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DayTimeSameStartEndDateReportQueryHandler(IReportsRepository repository)
    : GroupRowsReportQueryHandler<DayTimeSameStartEndDateReportQuery>(repository)
{
    protected override ReportTable Build(GroupRowsSnapshot snapshot)
    {
        return SameStartEndDateReports.DayTimeSameStartEndDate(snapshot);
    }
}
