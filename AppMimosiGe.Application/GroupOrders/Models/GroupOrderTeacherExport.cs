using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მასწავლებლის ხაზი ვიზარდი 2-ის ფაილში (D195). RowId: ჯგუფის მასწავლებლის სტრიქონი
///     (GroupsByTeachers.Id), რომელსაც ხაზი უკავშირდებოდა; null, თუ ასეთი არ იყო
/// </summary>
public sealed record GroupOrderTeacherExport(
    int Id,
    int TeacherContractId,
    int SalarySchemaId,
    DateTime? StartDate,
    DateTime? EndDate,
    int? RowId);
