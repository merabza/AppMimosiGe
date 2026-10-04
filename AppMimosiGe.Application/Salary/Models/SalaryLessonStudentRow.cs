using System;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     გაკვეთილის მოსწავლის სტრიქონი, რომელიც ჯგუფის მოსწავლის სტრიქონით გაკვეთილის ჯგუფს ეკუთვნის (Access-ის
///     VR16TSBase1ChargeDates-ის JOIN-ები). მასწავლებელი, შემცვლელი, სქემა, თარიღი და სტატუსი გაკვეთილისაა
/// </summary>
public sealed record SalaryLessonStudentRow(
    int LessonId,
    int GroupId,
    int TeacherContractId,
    int? SubstituteTeacherContractId,
    int SalarySchemeId,
    DateTime LessonDt,
    int LessonStatusId,
    float HoursCount);
