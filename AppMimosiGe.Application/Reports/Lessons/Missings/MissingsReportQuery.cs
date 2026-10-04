using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.Missings;

//r14Missings: გაცდენები
public sealed record MissingsReportQuery(DateTime StartDate, DateTime EndDate) : IQuery<ReportTable>;
