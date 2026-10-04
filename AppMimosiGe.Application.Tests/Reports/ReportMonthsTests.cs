using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportMonthsTests
{
    // the first day of the date's month, without the time (Access's DateSerial(Year(d), Month(d), 1))
    [Theory]
    [InlineData(1, 0)]
    [InlineData(15, 13)]
    [InlineData(31, 23)]
    public void Start_IsTheFirstDayOfTheMonth(int day, int hour)
    {
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
            ReportMonths.Start(new DateTime(2026, 10, day, hour, 45, 0, DateTimeKind.Unspecified)));
    }

    // GeoMonths' name and the year; a month without a name shows its number
    [Fact]
    public void Name_IsTheMonthsNameAndTheYear()
    {
        // Arrange
        Dictionary<int, string> names = new() { [9] = "სექტემბერი" };

        // Act & Assert
        Assert.Equal("სექტემბერი 2026",
            ReportMonths.Name(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), names));
        Assert.Equal("10 2027", ReportMonths.Name(new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Unspecified), names));
    }
}
