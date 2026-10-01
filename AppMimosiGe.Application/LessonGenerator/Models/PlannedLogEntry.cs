using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გენერატორის ლოგის ჩანაწერი: შეცდომის კოდი (ErrorLogTexts), დღე ან გაკვეთილის დრო და არსებული გაკვეთილი
/// </summary>
public sealed record PlannedLogEntry(int ErrorCode, DateTime? LessonDate, int? LessonId);
