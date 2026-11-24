using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანება ვიზარდი 2-ის ფაილში (D195): ცხრილის GroupOrders ყველა სვეტი, ტიპი რიცხვით (EGroupOrderType), და ხაზები
/// </summary>
public sealed record GroupOrderExport(
    int Id,
    int OrderType,
    int AcademicYearId,
    int? NumberYear,
    int? Number,
    DateTime? IssueDate,
    DateTime OrderDate,
    bool IsPaused,
    int? GroupId,
    int? TargetGroupId,
    string? GroupCode,
    int? CourseId,
    int? GroupSizeId,
    int? StudentStatusId,
    DateTime? VoidDate,
    string? OrganizationName,
    string? SignerTitle,
    string? SignerName,
    string CreatedByUserName,
    DateTime CreatedAt,
    string? ChangedByUserName,
    DateTime? ChangedAt,
    IReadOnlyList<GroupOrderTeacherExport> Teachers,
    IReadOnlyList<GroupOrderStudentExport> Students,
    IReadOnlyList<GroupOrderDayTimePlaceExport> DayTimePlaces);
