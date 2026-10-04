using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.WrongWeekDayChanges;

//r22: კვირის დღეების არასწორი ცვლილებები
public sealed record WrongWeekDayChangesReportQuery : IQuery<ReportTable>;
