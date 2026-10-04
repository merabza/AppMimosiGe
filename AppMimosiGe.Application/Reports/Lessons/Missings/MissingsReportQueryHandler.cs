using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.Missings;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class MissingsReportQueryHandler(IReportsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<MissingsReportQuery, ReportTable>
{
    //მხოლოდ დაწყებული გაკვეთილები (D121): მომავალი გაკვეთილის Present ჯერ false-ია, მაგრამ ის გაცდენა არ არის
    public async Task<Result<ReportTable>> Handle(MissingsReportQuery query, CancellationToken cancellationToken)
    {
        List<AbsenceCountRow> counts = await repository.GetAbsenceCounts(query.StartDate,
            LessonsReportPeriod.StartedBefore(query.EndDate, timeProvider), cancellationToken);
        return AbsenceReports.Missings(counts);
    }
}
