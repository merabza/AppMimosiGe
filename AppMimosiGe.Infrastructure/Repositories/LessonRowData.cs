using System;

namespace AppMimosiGe.Infrastructure.Repositories;

//გაკვეთილების სიის სტრიქონი SQL-ში, რომ დალაგება და გვერდებად დაყოფა სერვერზე მოხდეს (EF პროექციას member-init-ით
//თარგმნის)
internal sealed class LessonRowData
{
    public int LessonId { get; init; }
    public DateTime LessonDt { get; init; }
    public int GrpId { get; init; }
    public required string GroupCode { get; init; }
    public required string CourseName { get; init; }
    public required string TeacherName { get; init; }
    public string? SubstituteTeacherName { get; init; }
    public int LessonStatusId { get; init; }
    public required string LessonStatusName { get; init; }
    public int StudentsCount { get; init; }
    public int PresentCount { get; init; }
}
