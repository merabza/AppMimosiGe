using System;
using System.Globalization;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     მასწავლებლის დროების გადაფარვა სხვა ჯგუფთან (TeacherScheduleOverlapsRule): კვირის დღე, სხვა ჯგუფის გაკვეთილის
///     დრო და დღეები [FromDate, ToDate), როცა ორივე გაკვეთილი განრიგშია (ToDate null: დაუსრულებელი)
/// </summary>
public sealed record TeacherScheduleOverlap(
    int TeacherContractId,
    int OtherGrpId,
    string OtherGroupCode,
    int WeekDayId,
    TimeOnly OtherStartTime,
    TimeOnly OtherEndTime,
    DateTime FromDate,
    DateTime? ToDate)
{
    //"მასწავლებელი — ჯგუფი კოდი, დღე 15:00–16:30, 16.09.2026–05.10.2026"; დაუსრულებელი: "16.09.2026-დან"
    public string Describe(string teacherName, string weekDayName)
    {
        string days = ToDate is { } toDate
            ? string.Create(CultureInfo.InvariantCulture, $"{FromDate:dd.MM.yyyy}–{toDate:dd.MM.yyyy}")
            : string.Create(CultureInfo.InvariantCulture, $"{FromDate:dd.MM.yyyy}-დან");
        return string.Create(CultureInfo.InvariantCulture,
            $"{teacherName} — ჯგუფი {OtherGroupCode}, {weekDayName} {OtherStartTime:HH:mm}–{OtherEndTime:HH:mm}, {days}");
    }
}
