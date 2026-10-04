namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     ოთახის, მასწავლებლის ან მოსწავლის კონტრაქტის (OwnerId) გაკვეთილი; OwnerName რეპორტში ჩანს
/// </summary>
public sealed record ScheduleOccupancy(int OwnerId, string OwnerName, ScheduleLessonRow Lesson);
