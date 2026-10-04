using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.LessonsWithErrors;

//r13LessonsWithErrors: შეცდომიანი გაკვეთილები
public sealed record LessonsWithErrorsReportQuery : IQuery<ReportTable>;
