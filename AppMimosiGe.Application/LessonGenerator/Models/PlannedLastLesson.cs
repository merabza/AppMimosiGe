using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     „ბოლო გაკვეთილი": არსებული გაკვეთილი (LessonId) ან ახალი, რომელიც გეგმით იქმნება (LessonId null)
/// </summary>
public sealed record PlannedLastLesson(int? LessonId, DateTime LessonDt);
