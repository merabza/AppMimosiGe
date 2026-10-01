using System.Collections.Generic;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გაკვეთილის ცვლილება. Create: LessonId null, Values ახალი გაკვეთილია, Students მისი სტრიქონები (Add).
///     Update: Values ახალი მნიშვნელობებია, PreviousValues ძველი (null, თუ გაკვეთილის ველები არ იცვლება და მხოლოდ
///     მოსწავლეების სტრიქონები იცვლება). Delete: Values წასაშლელი გაკვეთილისაა, Students მისი სტრიქონები (Delete)
/// </summary>
public sealed record PlannedLessonChange(
    ELessonChangeKind Kind,
    int? LessonId,
    LessonValues Values,
    LessonValues? PreviousValues,
    IReadOnlyList<PlannedStudentRow> Students);
