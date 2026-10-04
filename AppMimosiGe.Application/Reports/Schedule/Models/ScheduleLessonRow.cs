using System;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     ჯგუფის განრიგის სტრიქონი (GroupDayTimePlaces): კვირის დღე, დაწყების დრო, საათები და ოთახი
/// </summary>
public sealed record ScheduleLessonRow(
    int GdtpId,
    int GroupId,
    int WeekDayId,
    TimeOnly StartTime,
    float HoursCount,
    int RoomId)
{
    public TimeSpan Start => StartTime.ToTimeSpan();

    //დაწყება + საათები (ნაწილ 14-ის D99-ის მსგავსად ზუსტი საათებით); შუაღამეს გადასვლისას 24 საათზე მეტია
    public TimeSpan End => Start + TimeSpan.FromHours(HoursCount);

    //დასრულების დრო საჩვენებლად (შუაღამის შემდეგ დღის დასაწყისიდან ითვლება)
    public TimeOnly EndTime => StartTime.Add(TimeSpan.FromHours(HoursCount));
}
