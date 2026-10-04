using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.TeacherMissAndSubstitutes;

//r34TeacherMissAndSubstitutes: გაუქმებები და ჩანაცვლებები
public sealed record TeacherMissAndSubstitutesReportQuery(DateTime StartDate, DateTime EndDate) : IQuery<ReportTable>;
