using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.WorkTime.TimeSheet;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class TimeSheetReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<TimeSheetReportQuery, ReportTable>
{
    //პერიოდის დღეები "თარიღამდე"-ს ჩათვლით
    public async Task<Result<ReportTable>> Handle(TimeSheetReportQuery query, CancellationToken cancellationToken)
    {
        WorkTimeData data = await repository.GetWorkTime(query.StartDate,
            LessonsReportPeriod.EndExclusive(query.EndDate), cancellationToken);
        IReadOnlyDictionary<int, string> monthNames = await repository.GetMonthNames(cancellationToken);
        return TimeSheetReport.TimeSheet(data, monthNames);
    }
}
