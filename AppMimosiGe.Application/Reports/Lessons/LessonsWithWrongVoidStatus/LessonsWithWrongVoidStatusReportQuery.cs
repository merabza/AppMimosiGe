using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.LessonsWithWrongVoidStatus;

//r12LessonsWithWrongVoidStatus: არასწორად გაუქმებული გაკვეთილები
public sealed record LessonsWithWrongVoidStatusReportQuery(DateTime StartDate, DateTime EndDate) : IQuery<ReportTable>;
