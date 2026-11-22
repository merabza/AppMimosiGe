using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მასწავლებლის სტრიქონი (GroupsByTeachers) პერიოდით [StartDate, EndDate). LineId: ბრძანების ხაზი, რომელმაც
///     სტრიქონი შექმნა (ბაზაში GroupOrderTeacherId)
/// </summary>
public sealed record GroupTeacherRow(
    int LineId,
    int TeacherContractId,
    int SalarySchemaId,
    DateTime StartDate,
    DateTime? EndDate);
