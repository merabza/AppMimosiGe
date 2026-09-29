using System;
using AppMimosiGe.Application.StudentContracts.Models;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.StudentContracts;

public sealed class CurrentAcademicYearTests
{
    private static readonly AcademicYear[] Years =
    [
        Year(10, "2025-2026", Day(2025, 9, 1), Day(2026, 9, 1)),
        Year(11, "2026-2027", Day(2026, 9, 1), Day(2027, 9, 1)),
        Year(12, "2028-2029", Day(2028, 9, 1), Day(2029, 9, 1))
    ];

    private static DateTime Day(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
    }

    private static AcademicYear Year(int id, string name, DateTime start, DateTime finish)
    {
        return new AcademicYear { AyId = id, AcademicYearName = name, StartDate = start, FinishDate = finish };
    }

    [Theory]
    [InlineData(2026, 9, 29, 11)]
    [InlineData(2026, 9, 1, 11)]
    [InlineData(2026, 8, 31, 10)]
    [InlineData(2025, 9, 1, 10)]
    public void Find_TodayInsideYear_ReturnsThatYear(int year, int month, int day, int expected)
    {
        Assert.Equal(expected, CurrentAcademicYear.Find(Years, Day(year, month, day)));
    }

    [Fact]
    public void Find_BetweenYears_ReturnsLastStartedYear()
    {
        Assert.Equal(11, CurrentAcademicYear.Find(Years, Day(2027, 12, 1)));
    }

    [Fact]
    public void Find_BeforeAllYears_ReturnsEarliestYear()
    {
        Assert.Equal(10, CurrentAcademicYear.Find(Years, Day(2020, 1, 1)));
    }

    [Fact]
    public void Find_NoYears_ReturnsNull()
    {
        Assert.Null(CurrentAcademicYear.Find([], Day(2026, 9, 29)));
    }
}
