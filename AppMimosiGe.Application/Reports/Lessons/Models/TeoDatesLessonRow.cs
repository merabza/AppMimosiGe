using System;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     გაკვეთილი თეორიული თარიღებით (r22): TeoMinDate / TeoMaxDate გენერატორი თვის განრიგით ითვლის
/// </summary>
public sealed record TeoDatesLessonRow(
    int LessonId,
    string GroupCode,
    SchedulePerson Teacher,
    DateTime TeoMinDate,
    DateTime TeoMaxDate,
    float FourWeekHours,
    DateTime LessonDt);
