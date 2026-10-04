using System;

namespace AppMimosiGe.Application.Reports.WorkTime.Models;

/// <summary>
///     სამუშაო საათების დასრულებული ჩანაწერი (WorkHours): თანამშრომელი, დაწყება, დასრულება
/// </summary>
public sealed record WorkTimeRecord(int WhId, int EmployeeId, DateTime Start, DateTime End);
