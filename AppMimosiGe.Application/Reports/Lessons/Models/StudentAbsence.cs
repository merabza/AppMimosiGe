using System;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     მოსწავლის კონტრაქტის გაცდენილი გაკვეთილი (LessonsByStudents, Present = false)
/// </summary>
public sealed record StudentAbsence(int StudentContractId, DateTime LessonDt);
