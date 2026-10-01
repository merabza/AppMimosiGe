using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     ჯგუფის განრიგის სტრიქონი (GroupDayTimePlaces) პერიოდით [StartDate, EndDate). WeekDayId: 1 = ორშაბათი ... 7 = კვირა
/// </summary>
public sealed record GeneratorDayTimePlaceRow(
    int WeekDayId,
    TimeOnly StartTime,
    float HoursCount,
    DateTime StartDate,
    DateTime? EndDate);
