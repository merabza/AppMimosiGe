using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მოსწავლის ხაზი ვიზარდი 2-ის ფაილში (D195). RowId: ჯგუფის მოსწავლის სტრიქონი (GroupsByStudents.GbsId),
///     რომელსაც ხაზი უკავშირდებოდა; null, თუ ასეთი არ იყო (მაგალითად გაყვანის ბრძანებაში)
/// </summary>
public sealed record GroupOrderStudentExport(
    int Id,
    int StudentContractId,
    float? FourWeekHours,
    decimal? FourWeekFee,
    decimal? OneHourFee,
    float? HoursCoefficient,
    string? Note,
    DateTime? StartDate,
    DateTime? EndDate,
    int? RowId);
