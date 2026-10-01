using System.Collections.Generic;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     ერთი ჯგუფის გენერაციის გეგმა. Logs ჩაენაცვლება ჯგუფის ძველ ლოგს. DirtyStudentContractIds: კონტრაქტები, რომლებსაც
///     DirtyNextPayDate უნდა დაენთოს. ClearDirtyLessons: ჯგუფის DirtyLessons ქრება (სრული გენერაცია, ჯგუფი ვალიდურია).
///     LastLesson მხოლოდ „ბოლო გაკვეთილის" რეჟიმშია
/// </summary>
public sealed record GroupLessonsPlan(
    IReadOnlyList<PlannedLessonChange> Changes,
    IReadOnlyList<PlannedLogEntry> Logs,
    IReadOnlyList<int> DirtyStudentContractIds,
    bool ClearDirtyLessons,
    PlannedLastLesson? LastLesson);
