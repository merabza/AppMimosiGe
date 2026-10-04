using System.Collections.Generic;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     განრიგი თარიღისთვის: მხოლოდ აქტიური ჯგუფები (Access-ის vActiveGroupsForReports) და მათი სტრიქონები, რომლებიც
///     ამ თარიღს მოქმედებს. WeekDays კვირის დღის ნომრის რიგითაა (crosstab-ის სვეტების ფიქსირებული რიგი)
/// </summary>
public sealed record ScheduleSnapshot(
    IReadOnlyList<ScheduleGroup> Groups,
    IReadOnlyList<ScheduleStudentRow> Students,
    IReadOnlyList<ScheduleTeacherRow> Teachers,
    IReadOnlyList<ScheduleLessonRow> Lessons,
    IReadOnlyDictionary<int, string> RoomNames,
    IReadOnlyList<ScheduleWeekDay> WeekDays,
    IReadOnlyDictionary<int, SchedulePerson> TeacherNames,
    IReadOnlyDictionary<int, SchedulePerson> StudentNames);
