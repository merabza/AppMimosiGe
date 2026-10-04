using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Schedule;

/// <summary>
///     განრიგის რეპორტების საერთო handler: თარიღის განრიგი ბაზიდან (მხოლოდ აქტიური ჯგუფები), შემდეგ რეპორტის სუფთა
///     ლოგიკა (Schedule/Models)
/// </summary>
public abstract class ScheduleReportQueryHandler<TQuery>(IReportsRepository repository)
    : IQueryHandler<TQuery, ReportTable> where TQuery : ScheduleReportQuery
{
    public async Task<Result<ReportTable>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        ScheduleSnapshot schedule = await repository.GetSchedule(query.Date, cancellationToken);
        return Build(schedule);
    }

    protected abstract ReportTable Build(ScheduleSnapshot schedule);
}
