using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.MissingsInRow;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class MissingsInRowReportQueryHandler(IReportsRepository repository, TimeProvider timeProvider)
    : IQueryHandler<MissingsInRowReportQuery, ReportTable>
{
    //გაცდენაც და ბოლო დასწრებაც მხოლოდ თარიღის ბოლომდე და მხოლოდ დაწყებული გაკვეთილებიდან (D121, D122)
    public async Task<Result<ReportTable>> Handle(MissingsInRowReportQuery query, CancellationToken cancellationToken)
    {
        MissingsInRowData data = await repository.GetMissingsInRow(query.Date,
            LessonsReportPeriod.StartedBefore(query.Date, timeProvider), cancellationToken);
        return AbsenceReports.MissingsInRow(data);
    }
}
