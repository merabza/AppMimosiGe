using System;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის მეორე მხარის სტრიქონის პერიოდი [StartDate, EndDate) (GroupSchedule.OtherSidePeriods, D201): მასწავლებლების
///     შემოწმებისას მოსწავლის სტრიქონი (GroupsByStudents), მოსწავლეების შემოწმებისას მასწავლებლისა (GroupsByTeachers)
/// </summary>
public sealed record GroupSchedulePeriod(DateTime StartDate, DateTime? EndDate);
