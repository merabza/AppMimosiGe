using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფი მასწავლებლების დროების შესამოწმებლად (TeacherScheduleOverlapsRule, D178): გაუქმების თარიღი, მასწავლებლების
///     და განრიგის სტრიქონები. შესანახ ახალ ჯგუფს GrpId 0 აქვს
/// </summary>
public sealed record TeacherScheduleGroup(
    int GrpId,
    string GroupCode,
    DateTime? VoidDate,
    IReadOnlyList<TeacherScheduleTeacher> Teachers,
    IReadOnlyList<TeacherScheduleDayTime> DayTimes);
