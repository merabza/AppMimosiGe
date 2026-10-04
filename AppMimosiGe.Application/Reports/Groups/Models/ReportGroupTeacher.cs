using System;

namespace AppMimosiGe.Application.Reports.Groups.Models;

/// <summary>
///     ჯგუფის მასწავლებლის სტრიქონი (GroupsByTeachers), რომელიც თარიღისთვის მოქმედებს: ხელფასის სქემა და დაწყება
/// </summary>
public sealed record ReportGroupTeacher(
    int Id,
    int GroupId,
    int TeacherContractId,
    string SalarySchemeName,
    DateTime StartDate);
