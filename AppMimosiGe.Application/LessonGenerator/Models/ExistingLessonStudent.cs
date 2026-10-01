namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     არსებული გაკვეთილის მოსწავლის სტრიქონი (LessonsByStudents). HasEnteredData: შეტანილია დასწრება, თემა, შეფასება ან
///     კომენტარი (Access-ის LessonStudentcontainsEnteredData)
/// </summary>
public sealed record ExistingLessonStudent(
    int Id,
    int StudentContractId,
    int? GroupByStudentId,
    float HoursCount,
    bool HasEnteredData);
