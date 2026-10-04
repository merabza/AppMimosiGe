using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.WorkTime.TimeSheet;

//r36: სამუშაო დროის აღრიცხვის ფორმა
public sealed record TimeSheetReportQuery(DateTime StartDate, DateTime EndDate) : IQuery<ReportTable>;
