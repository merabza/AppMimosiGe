using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     ჯგუფები თარიღისთვის (r08, r09, r10, r23, r24): აქტიური ჯგუფები (Access-ის vActiveGroupsForReports, D116) ზომით,
///     სტატუსით და საგნით, მათი ამ დღეს მოქმედი მოსწავლის (ტარიფით) და მასწავლებლის (ხელფასის სქემით) სტრიქონები და
///     ამ სტრიქონების კონტრაქტების სახელები
/// </summary>
public sealed record GroupsSnapshot(
    IReadOnlyList<ReportGroup> Groups,
    IReadOnlyList<ReportGroupStudent> Students,
    IReadOnlyList<ReportGroupTeacher> Teachers,
    IReadOnlyDictionary<int, SchedulePerson> StudentNames,
    IReadOnlyDictionary<int, SchedulePerson> TeacherNames);
