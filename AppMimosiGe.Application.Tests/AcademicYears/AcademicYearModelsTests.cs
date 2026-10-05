using System;
using System.Collections.Generic;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Application.StudentContracts.Models;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.AcademicYears;

public sealed class AcademicYearModelsTests
{
    private static DateTime Date(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);

    private static AcademicYear Year(int ayId, int startYear) => new()
    {
        AyId = ayId,
        AcademicYearName = $"{startYear}-{startYear + 1}",
        StartDate = Date(startYear, 9, 1),
        FinishDate = Date(startYear + 1, 9, 1)
    };

    // --- contract numbers: the year's last digit, a dot and the sequence

    [Theory]
    [InlineData(2026, "6")]
    [InlineData(2027, "7")]
    [InlineData(2030, "0")]
    public void Prefix_IsTheLastDigitOfTheStartYear(int year, string prefix)
    {
        Assert.Equal(prefix, ContractNumbers.Prefix(Date(year, 9, 1)));
    }

    [Fact]
    public void Next_EmptyYear_IsTheFirstNumber()
    {
        Assert.Equal("7.001", ContractNumbers.Next(Date(2027, 9, 1), []));
    }

    [Fact]
    public void Next_IsTheMaximumOfThePrefixPlusOne()
    {
        Assert.Equal("6.132", ContractNumbers.Next(Date(2026, 9, 1), ["6.001", "6.131", "6.010"]));
    }

    // another prefix (a contract entered with last year's number) or another format does not count
    [Fact]
    public void Next_IgnoresOtherPrefixesAndFormats()
    {
        Assert.Equal("7.003",
            ContractNumbers.Next(Date(2027, 9, 1), ["7.002", "6.500", "7.1", "7.0050", "x.999", "7-900", "17.900"]));
    }

    [Fact]
    public void Next_AfterTheLastNumber_IsNull()
    {
        Assert.Null(ContractNumbers.Next(Date(2027, 9, 1), ["7.998", "7.999"]));
    }

    [Fact]
    public void Next_BeforeTheLastNumber_IsTheLastOne()
    {
        Assert.Equal("7.999", ContractNumbers.Next(Date(2027, 9, 1), ["7.998"]));
    }

    // --- the next academic year

    [Fact]
    public void Plan_IsTheYearAfterTheCurrentOne()
    {
        NextAcademicYear? next = NextAcademicYear.Plan([Year(10, 2025), Year(11, 2026)], Date(2026, 10, 5));

        Assert.Equal(new NextAcademicYear("2027-2028", Date(2027, 9, 1), Date(2028, 9, 1)), next);
    }

    // after the new year is added the current year is still the old one, so the plan stays the same (idempotency)
    [Fact]
    public void Plan_AfterTheNewYearIsAdded_StaysTheSame()
    {
        List<AcademicYear> years = [Year(11, 2026), Year(12, 2027)];

        NextAcademicYear? next = NextAcademicYear.Plan(years, Date(2026, 10, 5));

        Assert.Equal("2027-2028", next!.AcademicYearName);
        Assert.True(next.ExistsIn(years));
    }

    [Fact]
    public void Plan_InTheNewYear_IsTheYearAfterIt()
    {
        Assert.Equal("2028-2029",
            NextAcademicYear.Plan([Year(11, 2026), Year(12, 2027)], Date(2027, 9, 1))!.AcademicYearName);
    }

    [Fact]
    public void Plan_WithoutYears_IsNull()
    {
        Assert.Null(NextAcademicYear.Plan([], Date(2026, 10, 5)));
    }

    // the finish date may carry a time: the new year starts on that day
    [Fact]
    public void Plan_StartsOnTheDayOfTheFinish()
    {
        AcademicYear year = Year(11, 2026);
        year.FinishDate = new DateTime(2027, 9, 1, 13, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(Date(2027, 9, 1), NextAcademicYear.Plan([year], Date(2026, 10, 5))!.StartDate);
    }

    [Theory]
    [InlineData("2027-2028")]
    [InlineData(" 2027-2028 ")]
    public void ExistsIn_ComparesTheTrimmedName(string name)
    {
        var next = new NextAcademicYear("2027-2028", Date(2027, 9, 1), Date(2028, 9, 1));
        AcademicYear year = Year(12, 2027);
        year.AcademicYearName = name;

        Assert.True(next.ExistsIn([year]));
        Assert.False(next.ExistsIn([Year(11, 2026)]));
    }
}
