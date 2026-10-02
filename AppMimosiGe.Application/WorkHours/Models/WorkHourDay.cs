using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     თანამშრომლის ჩანაწერის დღე: WhStart-ის თარიღი
/// </summary>
public sealed record WorkHourDay(int TeacherContractId, DateTime Day);
