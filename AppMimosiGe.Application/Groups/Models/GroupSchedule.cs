using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფი მასწავლებლების (D178) ან მოსწავლეების (D179) დროების შესამოწმებლად (GroupScheduleOverlapsRule): გაუქმების
///     თარიღი, შესამოწმებელი სტრიქონები (Members: მხოლოდ მასწავლებლების ან მხოლოდ მოსწავლეების) და განრიგის
///     სტრიქონები. შესანახ ახალ ჯგუფს GrpId 0 აქვს
/// </summary>
public sealed record GroupSchedule(
    int GrpId,
    string GroupCode,
    DateTime? VoidDate,
    IReadOnlyList<GroupScheduleMember> Members,
    IReadOnlyList<GroupScheduleDayTime> DayTimes);
