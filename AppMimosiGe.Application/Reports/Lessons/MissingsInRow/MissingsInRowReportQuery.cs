using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.MissingsInRow;

//r17MissingsInRow: ზედიზედ გაცდენები; მდგომარეობა Date დღისთვის (Access-ის EndDate, "თარიღისთვის")
public sealed record MissingsInRowReportQuery(DateTime Date) : IQuery<ReportTable>;
