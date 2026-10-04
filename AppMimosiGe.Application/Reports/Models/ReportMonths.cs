using System;
using System.Collections.Generic;
using System.Globalization;

namespace AppMimosiGe.Application.Reports.Models;

/// <summary>
///     თვის რეპორტების თარიღები და სათაურები (r01, r25, r36)
/// </summary>
public static class ReportMonths
{
    //თვის პირველი დღე, დროის გარეშე (Access-ის DateSerial(Year(d), Month(d), 1))
    public static DateTime Start(DateTime date)
    {
        return date.Date.AddDays(1 - date.Day);
    }

    //"თვე წელი", მაგ. "სექტემბერი 2026": სახელი GeoMonths-იდან (Access-ის join), მის გარეშე თვის ნომერი
    public static string Name(DateTime date, IReadOnlyDictionary<int, string> monthNames)
    {
        string month = monthNames.TryGetValue(date.Month, out string? name)
            ? name
            : date.Month.ToString(CultureInfo.InvariantCulture);
        return $"{month} {date.Year.ToString(CultureInfo.InvariantCulture)}";
    }
}
