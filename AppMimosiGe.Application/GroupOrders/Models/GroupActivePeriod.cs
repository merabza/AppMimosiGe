using System;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მოქმედი პერიოდი [StartDate, EndDate) (GroupActivity.ActivePeriods): ჯგუფი მოქმედია StartDate-იდან EndDate-ის
///     წინა დღის ჩათვლით; EndDate null დაუსრულებელს ნიშნავს
/// </summary>
public sealed record GroupActivePeriod(DateTime StartDate, DateTime? EndDate);
