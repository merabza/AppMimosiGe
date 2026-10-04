using System;

namespace AppMimosiGe.Application.Reports.WorkTime.Models;

/// <summary>
///     r36-ის გაკვეთილი (გაუქმებულის გარდა, მოსწავლეებით): თანამშრომელი (შემცვლელი, თუ არის), დრო, აღდგენის თარიღი
///     და საათები (მოსწავლეების საათების მაქსიმუმი, Access-ის Max(LessonsByStudents.HoursCount))
/// </summary>
public sealed record WorkTimeLesson(int LessonId, int EmployeeId, DateTime LessonDt, DateTime? RecoverDate, float Hours);
