using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მასწავლებლის ხაზი (GroupOrderTeachers). სტრიქონი, რომელსაც ხაზი ქმნის, მის Id-ს ინახავს (LineId).
///     StartDate და EndDate მხოლოდ საწყის ბრძანებაშია: სხვა ბრძანების სტრიქონი ბრძანების თარიღიდან იწყება
/// </summary>
public sealed record GroupOrderTeacherLine(
    int Id,
    int TeacherContractId,
    int SalarySchemaId,
    DateTime? StartDate = null,
    DateTime? EndDate = null)
{
    public GroupTeacherRow ToRow(DateTime startDate, DateTime? endDate)
    {
        return new GroupTeacherRow(Id, TeacherContractId, SalarySchemaId, startDate, endDate);
    }
}
