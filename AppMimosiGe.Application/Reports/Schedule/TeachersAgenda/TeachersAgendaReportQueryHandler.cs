using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeachersAgendaReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<TeachersAgendaReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleAgendaReports.TeachersAgenda(schedule);
    }
}
