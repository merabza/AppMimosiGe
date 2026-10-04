namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     ჯგუფის მასწავლებლის სტრიქონი (GroupsByTeachers)
/// </summary>
public sealed record ScheduleTeacherRow(int GbtId, int GroupId, int TeacherContractId);
