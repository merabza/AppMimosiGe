using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Groups;

/// <summary>
///     ჯგუფების რეპორტის query: მდგომარეობა Date დღისთვის (Access-ის EndDate, "თარიღისთვის")
/// </summary>
public abstract record GroupsReportQuery(DateTime Date) : IQuery<ReportTable>;
