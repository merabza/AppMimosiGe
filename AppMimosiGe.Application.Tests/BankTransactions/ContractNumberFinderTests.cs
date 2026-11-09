using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class ContractNumberFinderTests
{
    [Theory]
    [InlineData("ხელშეკრულება 6.150", "6.150")]
    [InlineData("6.150", "6.150")]
    [InlineData("N6.150.", "6.150")]
    [InlineData("#6-150 სწავლა", "6.150")]
    [InlineData("კონტრ. 6/001,", "6.001")]
    [InlineData("(5.120)", "5.120")]
    public void Find_FindsTheNumberInItsForms(string text, string expected)
    {
        Assert.Equal([expected], ContractNumberFinder.Find(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("16.150")]
    [InlineData("6.1500")]
    [InlineData("6.15")]
    [InlineData("150.00 ლარი")]
    [InlineData("06.11.2026")]
    [InlineData("1,6.150")]
    [InlineData("6.150.5")]
    [InlineData("6.150,5")]
    [InlineData("6 150")]
    public void Find_IgnoresOtherNumbers(string? text)
    {
        Assert.Empty(ContractNumberFinder.Find(text));
    }

    [Fact]
    public void Find_ReturnsEveryNumberOnce()
    {
        Assert.Equal(["6.150", "6.017"], ContractNumberFinder.Find("6.150 და 6.017, ისევ 6-150"));
    }
}
