using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherDoubleGroupsReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<TeacherDoubleGroupsReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleDoubleReports.TeacherDoubleGroups(schedule);
    }
}
