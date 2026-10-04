using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class StudentDoubleCoursesReportQueryHandler(IReportsRepository repository)
    : ScheduleReportQueryHandler<StudentDoubleCoursesReportQuery>(repository)
{
    protected override ReportTable Build(ScheduleSnapshot schedule)
    {
        return ScheduleDoubleReports.StudentDoubleCourses(schedule);
    }
}
