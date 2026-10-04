using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ჯგუფების ყველა სტრიქონი შემოწმების რეპორტებისთვის (r26–r33): ჯგუფები, მოსწავლის (ტარიფით), მასწავლებლის
///     (ხელფასის სქემით) და განრიგის სტრიქონები თარიღების მიუხედავად, ყველა სასწავლო წლისა (D125). TeacherContracts
///     მასწავლებლის სტრიქონების კონტრაქტებია ძირითადი სქემით, MaxFinishDate Access-ის vMaxDate-ია (სასწავლო წლების
///     ბოლო დასასრული)
/// </summary>
public sealed record GroupRowsSnapshot(
    IReadOnlyList<CheckGroup> Groups,
    IReadOnlyList<CheckStudentRow> Students,
    IReadOnlyList<CheckTeacherRow> Teachers,
    IReadOnlyList<CheckDayTimeRow> DayTimes,
    IReadOnlyDictionary<int, SchedulePerson> StudentNames,
    IReadOnlyDictionary<int, CheckTeacherContract> TeacherContracts,
    IReadOnlyDictionary<int, string> SalarySchemeNames,
    DateTime? MaxFinishDate);
