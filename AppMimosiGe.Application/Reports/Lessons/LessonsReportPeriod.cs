using System;
using System.Linq;

namespace AppMimosiGe.Application.Reports.Lessons;

/// <summary>
///     გაკვეთილების რეპორტების პერიოდის ზღვარი. "თარიღამდე" ჩათვლითაა: მომდევნო დღის დასაწყისამდე (Access-ის
///     txtEndDate-ის ნაგულისხმევი დღის 23:59:59-ია). "მხოლოდ დაწყებული გაკვეთილები" (D121) ამ ზღვარსა და ახლანდელ
///     დროს შორის უფრო ადრეულამდე ითვლის
/// </summary>
public static class LessonsReportPeriod
{
    public static DateTime EndExclusive(DateTime endDate)
    {
        return endDate.Date.AddDays(1);
    }

    //გაკვეთილის დრო ბაზაში ადგილობრივია (Access-იდან), ამიტომ ახლანდელი დროც ადგილობრივია
    public static DateTime StartedBefore(DateTime endDate, TimeProvider timeProvider)
    {
        DateTime[] limits = [timeProvider.GetLocalNow().DateTime, EndExclusive(endDate)];
        return limits.Min();
    }
}
