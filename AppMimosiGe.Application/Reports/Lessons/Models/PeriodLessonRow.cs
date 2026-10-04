using System;
using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     პერიოდის გაკვეთილი r11-ის, r12-ის და r34-ისთვის: მასწავლებელი (Lessons.TeacherContractID) და შემცვლელი
///     კონტრაქტის ნომრით, სტატუსი, აღდგენის თარიღის და დამსწრე მოსწავლის (Access-ის vLessonsWithPresentStudents)
///     არსებობა
/// </summary>
public sealed record PeriodLessonRow(
    int LessonId,
    DateTime LessonDt,
    string GroupCode,
    int TeacherContractId,
    SchedulePerson Teacher,
    SchedulePerson? SubstituteTeacher,
    int LessonStatusId,
    string LessonStatusName,
    bool HasRecoverDate,
    bool HasPresentStudent);
