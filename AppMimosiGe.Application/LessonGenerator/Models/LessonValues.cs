using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გაკვეთილის ველები, რომლებსაც გენერატორი ადგენს და არსებულ გაკვეთილს ადარებს (Access-ის
///     LessonData.isOnlyLessonEqualTo)
/// </summary>
public sealed record LessonValues(
    DateTime LessonDt,
    int TeacherContractId,
    int SalarySchemaId,
    float FourWeekHours,
    DateTime TeoMinDate,
    DateTime TeoMaxDate);
