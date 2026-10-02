using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     ავტომატური დაგენერირებით შესაქმნელი ჩანაწერი
/// </summary>
public sealed record GeneratedWorkHour(int TeacherContractId, DateTime WhStart, DateTime WhEnd);
