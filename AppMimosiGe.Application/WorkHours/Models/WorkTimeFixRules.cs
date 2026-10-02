using System;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     "სამუშაოს დაწყების" და "დასრულების" წესები (Access-ის FrmWorkHours.cmdFixWorkStart / cmdFixWorkEnd)
/// </summary>
public static class WorkTimeFixRules
{
    //ლუფტი ამაზე მეტი ვერ იქნება
    public const int MaxLuftMinutes = 30;

    //სერვერის ადგილობრივი (საქართველოს) დრო წამის სიზუსტით, როგორც Access-ის Now()
    public static DateTime Now(TimeProvider timeProvider)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
    }

    //Access: Nz(txtLuft, 0), უარყოფითი 0 ხდება (30-ზე მეტს ვალიდატორი არ უშვებს)
    public static int LuftMinutes(int? luftMinutes)
    {
        return Math.Max(luftMinutes ?? 0, 0);
    }
}
