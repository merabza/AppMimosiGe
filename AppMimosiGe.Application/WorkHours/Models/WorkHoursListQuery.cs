using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     ნამუშევარი დროის სიის ერთი გვერდის მოთხოვნა: ფილტრი (თანამშრომელი, თარიღის შუალედი), დალაგება და გვერდი.
///     DateFrom და DateTo დღეებია, ორივე ჩათვლით
/// </summary>
public sealed record WorkHoursListQuery(
    int Offset,
    int RowsCount,
    int? TeacherContractId,
    DateTime? DateFrom,
    DateTime? DateTo,
    IReadOnlyList<WorkHourSortField> SortFields);
