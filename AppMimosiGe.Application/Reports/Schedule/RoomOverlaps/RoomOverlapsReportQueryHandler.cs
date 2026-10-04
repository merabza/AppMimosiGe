using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RoomOverlapsReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<RoomOverlapsReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleOverlapReports.RoomOverlaps(schedule);
    }
}
