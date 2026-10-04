using System;

namespace AppMimosiGe.Application.Reports.Checks.Models;

/// <summary>
///     ჯგუფის განრიგის სტრიქონი (GroupDayTimePlaces, Access-ის "დღეების განაწილება") საათებით
/// </summary>
public sealed record CheckDayTimeRow(int GdtpId, int GroupId, DateTime StartDate, DateTime? EndDate, float HoursCount);
