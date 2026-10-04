using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UsedDayTimesReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<UsedDayTimesReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleAgendaReports.UsedDayTimes(schedule);
    }
}
