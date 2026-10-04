using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Reports.Comments.DailyComments;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DailyCommentsReportQueryHandler(IReportsRepository repository)
    : IQueryHandler<DailyCommentsReportQuery, ReportTable>
{
    //თარიღის თვის ყველა გაკვეთილი (Access-ის Year/Month(txtEndDate)); მასწავლებელი გაკვეთილის მასწავლებელია
    public async Task<Result<ReportTable>> Handle(DailyCommentsReportQuery query, CancellationToken cancellationToken)
    {
        DateTime monthStart = ReportMonths.Start(query.Date);
        List<CommentLesson> lessons = await repository.GetCommentLessons(monthStart, monthStart.AddMonths(1),
            query.TeacherContractId, cancellationToken);
        return CommentReports.DailyComments(lessons);
    }
}
