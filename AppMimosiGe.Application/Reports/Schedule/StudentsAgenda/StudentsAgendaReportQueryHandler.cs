using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentsAgendaReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<StudentsAgendaReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleAgendaReports.StudentsAgenda(schedule);
    }
}
