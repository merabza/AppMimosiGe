using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.WrongStatusLessons;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class WrongStatusLessonsReportQueryHandler(IReportsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<WrongStatusLessonsReportQuery, ReportTable>
{
    //მხოლოდ დაწყებული გაკვეთილები (D121): მომავალ გაკვეთილს დასწრება ჯერ არ აქვს
    public async Task<Result<ReportTable>> Handle(WrongStatusLessonsReportQuery query,
        CancellationToken cancellationToken)
    {
        List<PeriodLessonRow> lessons = await repository.GetPeriodLessons(query.StartDate,
            LessonsReportPeriod.StartedBefore(query.EndDate, timeProvider), cancellationToken);
        return LessonCheckReports.WrongStatusLessons(lessons);
    }
}
