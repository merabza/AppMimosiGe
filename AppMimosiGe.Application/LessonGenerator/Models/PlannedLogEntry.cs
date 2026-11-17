using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გენერატორის ლოგის ჩანაწერი: შეცდომის კოდი (ErrorLogTexts), დღე ან გაკვეთილის დრო და არსებული გაკვეთილი.
///     LessonStudentId: შეცდომა 14-ის ზედმეტი მოსწავლის სტრიქონი. ის ბაზის ლოგში არ იწერება, მას შეცდომიანი
///     გაკვეთილების წესი იყენებს (LessonsWithErrorsRule)
/// </summary>
public sealed record PlannedLogEntry(int ErrorCode, DateTime? LessonDate, int? LessonId, int? LessonStudentId = null);
