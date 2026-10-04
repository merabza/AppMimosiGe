using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.TeacherMissAndSubstitutes;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TeacherMissAndSubstitutesReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<TeacherMissAndSubstitutesReportQuery, ReportTable>
{
    //პერიოდის ყველა გაკვეთილი, მომავალიც (Access-ივით): გაუქმება და შემცვლელი წინასწარ იწერება
    public async Task<Result<ReportTable>> Handle(TeacherMissAndSubstitutesReportQuery query,
        CancellationToken cancellationToken)
    {
        List<PeriodLessonRow> lessons = await repository.GetPeriodLessons(query.StartDate,
            LessonsReportPeriod.EndExclusive(query.EndDate), cancellationToken);
        return LessonCheckReports.TeacherMissAndSubstitutes(lessons);
    }
}
