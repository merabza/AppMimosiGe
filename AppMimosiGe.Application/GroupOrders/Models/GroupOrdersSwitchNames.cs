using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 1-ის რეპორტის სახელები (D194): მასწავლებლები და მოსწავლეები კონტრაქტის Id-ით („გვარი სახელი / ნომერი",
///     როგორც ჯგუფის ფორმაში), კვირის დღეები და გაკვეთილის დაწყების დროები Id-ით
/// </summary>
public sealed record GroupOrdersSwitchNames(
    IReadOnlyDictionary<int, string> Teachers,
    IReadOnlyDictionary<int, string> Students,
    IReadOnlyDictionary<int, string> WeekDays,
    IReadOnlyDictionary<int, TimeOnly> LessonStartTimes);
