using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის განრიგის სტრიქონი (GroupDayTimePlaces) პერიოდით [StartDate, EndDate). LineId: ბრძანების ხაზი, რომელმაც
///     სტრიქონი შექმნა (ბაზაში GroupOrderDayTimePlaceId)
/// </summary>
public sealed record GroupDayTimePlaceRow(
    int LineId,
    int WeekDayId,
    int LessonStartTimeId,
    float HoursCount,
    int RoomId,
    DateTime StartDate,
    DateTime? EndDate);
