using System;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის მასწავლებლის (GroupsByTeachers) ან მოსწავლის (GroupsByStudents) სტრიქონი: კონტრაქტი და პერიოდი
///     [StartDate, EndDate)
/// </summary>
public sealed record GroupScheduleMember(int ContractId, DateTime StartDate, DateTime? EndDate);
