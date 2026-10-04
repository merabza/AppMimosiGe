using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Lessons.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Lessons.LessonsWithErrors;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class LessonsWithErrorsReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<LessonsWithErrorsReportQuery, ReportTable>
{
    public async Task<Result<ReportTable>> Handle(LessonsWithErrorsReportQuery query,
        CancellationToken cancellationToken)
    {
        List<LessonErrorRow> errors = await repository.GetLessonErrors(cancellationToken);
        return LessonCheckReports.LessonsWithErrors(errors);
    }
}
