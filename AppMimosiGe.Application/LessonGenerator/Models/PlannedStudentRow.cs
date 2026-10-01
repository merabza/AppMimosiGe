namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     გაკვეთილის მოსწავლის სტრიქონის ცვლილება. RowId არსებული სტრიქონისაა (Add-ში null). Update-ში GroupByStudentId და
///     HoursCount ახალი მნიშვნელობებია, Delete-ში წასაშლელი სტრიქონისა
/// </summary>
public sealed record PlannedStudentRow(
    EStudentRowChangeKind Kind,
    int? RowId,
    int StudentContractId,
    int? GroupByStudentId,
    float HoursCount);
