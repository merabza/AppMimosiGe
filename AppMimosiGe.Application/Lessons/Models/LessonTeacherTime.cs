using System;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     ვინ ატარებს გაკვეთილს და როდის (TeacherLessonOverlapsRule.TeacherTime): მასწავლებლის კონტრაქტი, ფაქტობრივი
///     დაწყება და საათები
/// </summary>
public sealed record LessonTeacherTime(int TeacherContractId, DateTime Start, float Hours)
{
    public DateTime End => Start.AddHours(Hours);
}
