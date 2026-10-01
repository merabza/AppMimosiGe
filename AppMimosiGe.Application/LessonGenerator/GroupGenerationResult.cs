using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Responses;

namespace AppMimosiGe.Application.LessonGenerator;

/// <summary>
///     ერთი ჯგუფის გენერაციის შედეგი. LastLessonId: „ბოლო გაკვეთილის" ID (ახალი გაკვეთილისა შენახვის შემდეგ, dry-run-ში
///     null)
/// </summary>
public sealed record GroupGenerationResult(
    GroupLessonsGenerationResponse Response,
    PlannedLastLesson? LastLesson,
    int? LastLessonId);
