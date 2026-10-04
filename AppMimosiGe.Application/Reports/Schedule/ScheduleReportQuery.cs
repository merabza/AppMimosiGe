using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Schedule;

/// <summary>
///     განრიგის რეპორტის query: მდგომარეობა Date დღისთვის (Access-ის EndDate, "თარიღისთვის")
/// </summary>
public abstract record ScheduleReportQuery(DateTime Date) : IQuery<ReportTable>;
