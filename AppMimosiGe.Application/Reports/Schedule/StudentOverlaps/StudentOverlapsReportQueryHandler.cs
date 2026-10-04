using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentOverlapsReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<StudentOverlapsReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleOverlapReports.StudentOverlaps(schedule);
    }
}
