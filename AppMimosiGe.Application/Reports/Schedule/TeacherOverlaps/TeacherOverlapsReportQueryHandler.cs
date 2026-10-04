using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherOverlapsReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<TeacherOverlapsReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleOverlapReports.TeacherOverlaps(schedule);
    }
}
