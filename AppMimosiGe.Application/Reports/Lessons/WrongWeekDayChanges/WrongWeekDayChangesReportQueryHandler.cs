using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.WrongWeekDayChanges;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class WrongWeekDayChangesReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<WrongWeekDayChangesReportQuery, ReportTable>
{
    public async Task<Result<ReportTable>> Handle(WrongWeekDayChangesReportQuery query,
        CancellationToken cancellationToken)
    {
        List<TeoDatesLessonRow> lessons = await repository.GetLessonsWithMidnightTeoDates(cancellationToken);
        return LessonCheckReports.WrongWeekDayChanges(lessons);
    }
}
