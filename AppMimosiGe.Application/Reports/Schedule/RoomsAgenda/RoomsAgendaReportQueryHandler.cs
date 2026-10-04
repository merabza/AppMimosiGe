using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RoomsAgendaReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<RoomsAgendaReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleAgendaReports.RoomsAgenda(schedule);
    }
}
