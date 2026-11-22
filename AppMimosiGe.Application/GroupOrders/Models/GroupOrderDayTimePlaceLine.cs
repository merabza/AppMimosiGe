using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების განრიგის ხაზი (GroupOrderDayTimePlaces). სტრიქონი, რომელსაც ხაზი ქმნის, მის Id-ს ინახავს (LineId).
///     StartDate და EndDate მხოლოდ საწყის ბრძანებაშია
/// </summary>
public sealed record GroupOrderDayTimePlaceLine(
    int Id,
    int WeekDayId,
    int LessonStartTimeId,
    float HoursCount,
    int RoomId,
    DateTime? StartDate = null,
    DateTime? EndDate = null)
{
    public GroupDayTimePlaceRow ToRow(DateTime startDate, DateTime? endDate)
    {
        return new GroupDayTimePlaceRow(Id, WeekDayId, LessonStartTimeId, HoursCount, RoomId, startDate, endDate);
    }
}
