using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.LessonsWithWrongVoidStatus;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class LessonsWithWrongVoidStatusReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<LessonsWithWrongVoidStatusReportQuery, ReportTable>
{
    //პერიოდის ყველა გაკვეთილი, მომავალიც (Access-ივით): გაუქმებაც და აღდგენის თარიღიც წინასწარ შეიძლება ჩაიწეროს
    public async Task<Result<ReportTable>> Handle(LessonsWithWrongVoidStatusReportQuery query,
        CancellationToken cancellationToken)
    {
        List<PeriodLessonRow> lessons = await repository.GetPeriodLessons(query.StartDate,
            LessonsReportPeriod.EndExclusive(query.EndDate), cancellationToken);
        return LessonCheckReports.LessonsWithWrongVoidStatus(lessons);
    }
}
