using System;
using AppMimosiGe.Infrastructure.Time;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class GeorgiaTimeProviderTests
{
    [Fact]
    public void LocalTimeZone_IsTbilisi()
    {
        // Act
        TimeZoneInfo timeZone = new GeorgiaTimeProvider().LocalTimeZone;

        // Assert
        Assert.Equal(GeorgiaTimeProvider.TimeZoneId, timeZone.Id);
    }

    // Georgia has no daylight saving time, so winter and summer both are UTC+4
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void LocalTimeZone_IsUtcPlusFourAllYear(int month)
    {
        // Arrange
        var utc = new DateTime(2026, month, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        TimeSpan offset = new GeorgiaTimeProvider().LocalTimeZone.GetUtcOffset(utc);

        // Assert
        Assert.Equal(TimeSpan.FromHours(4), offset);
    }

    // a UTC server's late evening is already the next day in Tbilisi
    [Fact]
    public void LocalTimeZone_TurnsLateUtcEveningIntoTheNextGeorgianDay()
    {
        // Arrange
        var utc = new DateTimeOffset(2026, 10, 31, 21, 30, 0, TimeSpan.Zero);

        // Act
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utc, new GeorgiaTimeProvider().LocalTimeZone);

        // Assert
        Assert.Equal(new DateTime(2026, 11, 1, 1, 30, 0, DateTimeKind.Unspecified), local.DateTime);
    }

    [Fact]
    public void GetLocalNow_HasTheGeorgianOffset()
    {
        // Act
        DateTimeOffset now = new GeorgiaTimeProvider().GetLocalNow();

        // Assert
        Assert.Equal(TimeSpan.FromHours(4), now.Offset);
    }
}
