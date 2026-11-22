using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფი მასწავლებლების (D178) ან მოსწავლეების (D179) დროების შესამოწმებლად (GroupScheduleOverlapsRule): გაუქმების
///     თარიღი, შესამოწმებელი სტრიქონები (Members: მხოლოდ მასწავლებლების ან მხოლოდ მოსწავლეების), განრიგის სტრიქონები
///     და მეორე მხარის ყველა სტრიქონის პერიოდი (OtherSidePeriods, D201: მასწავლებლების შემოწმებისას ჯგუფის ყველა
///     მოსწავლისა, მოსწავლეების შემოწმებისას ყველა მასწავლებლისა). შესანახ ახალ ჯგუფს GrpId 0 აქვს
/// </summary>
public sealed record GroupSchedule(
    int GrpId,
    string GroupCode,
    DateTime? VoidDate,
    IReadOnlyList<GroupScheduleMember> Members,
    IReadOnlyList<GroupScheduleDayTime> DayTimes,
    IReadOnlyList<GroupSchedulePeriod> OtherSidePeriods);
