using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     გაკვეთილის ერთი მოსწავლის სტრიქონის დრო: გაკვეთილის დაწყება და მოსწავლის საათები (LessonsByStudents.HoursCount)
/// </summary>
public sealed record LessonTimeData(DateTime LessonDt, float HoursCount);
