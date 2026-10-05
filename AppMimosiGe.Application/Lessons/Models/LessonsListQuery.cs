using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     გაკვეთილების სიის ერთი გვერდის მოთხოვნა: ფილტრი, დალაგება და გვერდი.
///     TeacherContractId გაკვეთილს მასწავლებლითაც პოულობს და შემცვლელითაც. DateFrom და DateTo დღეებია, ორივე
///     ჩათვლით. Unfilled: მხოლოდ შეუვსებელი გაკვეთილები; "წარსული" Now-ით მოწმდება. AcademicYearId: ჯგუფის სასწავლო წელი
///     (ნაწილი 20)
/// </summary>
public sealed record LessonsListQuery(
    int Offset,
    int RowsCount,
    DateTime Now,
    int? GrpId,
    int? TeacherContractId,
    DateTime? DateFrom,
    DateTime? DateTo,
    int? LessonStatusId,
    bool Unfilled,
    IReadOnlyList<LessonSortField> SortFields,
    int? AcademicYearId = null);
