namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     ჯგუფის მოსწავლის სტრიქონი (GroupsByStudents)
/// </summary>
public sealed record ScheduleStudentRow(int GbsId, int GroupId, int StudentContractId);
