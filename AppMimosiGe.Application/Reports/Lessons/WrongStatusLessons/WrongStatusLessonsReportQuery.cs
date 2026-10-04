using System;
using AppMimosiGe.Application.Reports.Models;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.Reports.Lessons.WrongStatusLessons;

//r11WrongStatuseLessons: გასაუქმებელი გაკვეთილები
public sealed record WrongStatusLessonsReportQuery(DateTime StartDate, DateTime EndDate) : IQuery<ReportTable>;
