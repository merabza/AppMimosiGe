using System;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის განრიგის სტრიქონი (GroupDayTimePlaces): კვირის დღე, დაწყების დრო, საათები და პერიოდი [StartDate, EndDate)
/// </summary>
public sealed record TeacherScheduleDayTime(
    int WeekDayId,
    TimeOnly StartTime,
    float HoursCount,
    DateTime StartDate,
    DateTime? EndDate)
{
    public TimeSpan Start => StartTime.ToTimeSpan();

    //დაწყება + საათები, როგორც r18-ში (ScheduleLessonRow); შუაღამეს გადასვლისას 24 საათზე მეტია
    public TimeSpan End => Start + TimeSpan.FromHours(HoursCount);

    //დასრულების დრო საჩვენებლად (შუაღამის შემდეგ დღის დასაწყისიდან ითვლება)
    public TimeOnly EndTime => StartTime.Add(TimeSpan.FromHours(HoursCount));
}
