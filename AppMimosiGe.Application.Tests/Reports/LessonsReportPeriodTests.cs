using System;
using AppMimosiGe.Application.Reports.Lessons;
using Moq;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class LessonsReportPeriodTests
{
    private static readonly DateTime EndDate = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime NextMidnight = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);

    //a provider whose local now is the given time
    private static TimeProvider At(DateTime localNow)
    {
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(t => t.LocalTimeZone).Returns(TimeZoneInfo.Utc);
        timeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(localNow, TimeSpan.Zero));
        return timeProvider.Object;
    }

    // "თარიღამდე" includes the whole end date, whatever the time in it
    [Fact]
    public void EndExclusive_IsTheMidnightAfterTheEndDate()
    {
        Assert.Equal(NextMidnight, LessonsReportPeriod.EndExclusive(EndDate.AddHours(23).AddMinutes(59)));
    }

    // only the lessons that have started (D121): now, while it is earlier than the end of the end date
    [Fact]
    public void StartedBefore_NowOnTheEndDate_IsNow()
    {
        DateTime now = EndDate.AddHours(15.5);

        Assert.Equal(now, LessonsReportPeriod.StartedBefore(EndDate, At(now)));
    }

    [Fact]
    public void StartedBefore_NowAfterTheEndDate_IsTheEndOfTheEndDate()
    {
        Assert.Equal(NextMidnight, LessonsReportPeriod.StartedBefore(EndDate, At(NextMidnight.AddDays(3))));
    }

    // now exactly at the next midnight: both limits are the same moment
    [Fact]
    public void StartedBefore_NowAtTheNextMidnight_IsThatMidnight()
    {
        Assert.Equal(NextMidnight, LessonsReportPeriod.StartedBefore(EndDate, At(NextMidnight)));
    }

    // a future end date: the lessons up to now
    [Fact]
    public void StartedBefore_EndDateInTheFuture_IsNow()
    {
        DateTime now = EndDate.AddDays(-10).AddHours(9);

        Assert.Equal(now, LessonsReportPeriod.StartedBefore(EndDate, At(now)));
    }
}
