using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     ჩანაწერის თანამშრომელი და დრო, ჯამური საათების დასათვლელად
/// </summary>
public sealed record WorkHourTimeData(int TeacherContractId, string EmployeeName, DateTime WhStart, DateTime? WhEnd);
