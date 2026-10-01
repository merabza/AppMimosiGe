using System.Collections.Generic;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     ჯგუფის არსებული გაკვეთილი მოსწავლეების სტრიქონებით
/// </summary>
public sealed record ExistingLesson(int Id, LessonValues Values, IReadOnlyList<ExistingLessonStudent> Students);
