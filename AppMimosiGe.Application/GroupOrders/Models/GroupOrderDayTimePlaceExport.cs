using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების განრიგის ხაზი ვიზარდი 2-ის ფაილში (D195). RowId: ჯგუფის განრიგის სტრიქონი
///     (GroupDayTimePlaces.GdtpId), რომელსაც ხაზი უკავშირდებოდა; null, თუ ასეთი არ იყო
/// </summary>
public sealed record GroupOrderDayTimePlaceExport(
    int Id,
    int WeekDayId,
    int LessonStartTimeId,
    float HoursCount,
    int RoomId,
    DateTime? StartDate,
    DateTime? EndDate,
    int? RowId);
