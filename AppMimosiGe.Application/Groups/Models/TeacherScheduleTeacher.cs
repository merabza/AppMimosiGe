using System;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის მასწავლებლის სტრიქონი (GroupsByTeachers) პერიოდით [StartDate, EndDate)
/// </summary>
public sealed record TeacherScheduleTeacher(int TeacherContractId, DateTime StartDate, DateTime? EndDate);
