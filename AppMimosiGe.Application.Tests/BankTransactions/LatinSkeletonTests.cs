using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class LatinSkeletonTests
{
    [Fact]
    public void Tokens_EmptyText_HasNoTokens()
    {
        Assert.Empty(LatinSkeleton.Tokens(null));
        Assert.Empty(LatinSkeleton.Tokens(string.Empty));
        Assert.Empty(LatinSkeleton.Tokens(" ,.- 123 "));
    }

    [Fact]
    public void Tokens_SplitsOnEverythingThatIsNotALetter()
    {
        Assert.Equal(["giorgi", "beridze", "sts", "a"], LatinSkeleton.Tokens("გიორგი ბერიძე,  სწ.ა-150"));
    }

    [Theory]
    [InlineData("აბგდევზთიკლმნოპ", "abgdevztiklmnop")]
    [InlineData("ჟრსტუფქღყშჩცძწჭხჯჰ", "zhrstupkgkshchtsdztschkjh")]
    public void Tokens_TransliteratesEveryGeorgianLetter(string georgian, string expected)
    {
        Assert.Equal([expected], LatinSkeleton.Tokens(georgian));
    }

    [Theory]
    [InlineData("ბერიძე", "BERIDZE")]
    [InlineData("გოგიჩაიშვილი", "Gogitchaishvili")]
    [InlineData("ციკლაური", "Ciklauri")]
    [InlineData("ციკლაური", "Tsiklauri")]
    [InlineData("ციკლაური", "Tziklauri")]
    [InlineData("ხარაძე", "Kharadze")]
    [InlineData("ხარაძე", "Xaradze")]
    [InlineData("ყიფიანი", "Qipiani")]
    [InlineData("ყიფიანი", "Kipiani")]
    [InlineData("ღლონტი", "Ghlonti")]
    [InlineData("ფხაკაძე", "Phkhakadze")]
    [InlineData("თოდუა", "Thodua")]
    [InlineData("ჯანელიძე", "Djanelidze")]
    [InlineData("ვაჩნაძე", "Watchnadze")]
    [InlineData("ჩიქოვანი", "Chikovani")]
    [InlineData("მაია", "Maya")]
    public void Tokens_GeorgianAndLatinSpellingsHaveTheSameSkeleton(string georgian, string latin)
    {
        Assert.Equal(LatinSkeleton.Tokens(georgian), LatinSkeleton.Tokens(latin));
    }
}
