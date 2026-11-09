using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class NameMatcherTests
{
    [Theory]
    [InlineData("ბერიძე")]
    [InlineData("ბერიძეს")]
    [InlineData("ბერიძის")]
    [InlineData("ბერიძისთვის")]
    [InlineData("ბერიძისათვის")]
    [InlineData("ბერიძით")]
    [InlineData("ბერიძეზე")]
    [InlineData("ბერიძესთან")]
    [InlineData("ბერიძისგან")]
    [InlineData("ბერიძეებს")]
    [InlineData("BERIDZE")]
    [InlineData("Beridzes")]
    public void ContainsName_FindsTheSurnameInItsCaseForms(string text)
    {
        Assert.True(NameMatcher.ContainsName(LatinSkeleton.Tokens(text), "ბერიძე"));
    }

    [Theory]
    [InlineData("გელაშვილი", "გელაშვილის")]
    [InlineData("გელაშვილი", "გელაშვილისათვის")]
    [InlineData("გელაშვილი", "გელაშვილს")]
    [InlineData("ბერია", "ბერიას")]
    [InlineData("ბერია", "ბერიასთვის")]
    [InlineData("ბერია", "ბერიის")]
    [InlineData("ნინო", "ნინოს")]
    [InlineData("ნინო", "ნინოსთვის")]
    [InlineData("გიორგი", "გიორგიმ")]
    [InlineData("გიორგი", "გიორგის")]
    [InlineData("გოგოლაძე", "გოგოლაძეებისთვის")]
    public void ContainsName_FindsOtherDeclensions(string name, string text)
    {
        Assert.True(NameMatcher.ContainsName(LatinSkeleton.Tokens(text), name));
    }

    [Theory]
    [InlineData("ბერიძე", "ბერიძიშვილი")]
    [InlineData("ბერიძე", "ბერი")]
    [InlineData("ბერი", "ბერიძე")]
    [InlineData("ბერიძე", "ბერძენი")]
    [InlineData("ნინო", "ნინოშვილი")]
    [InlineData("ანა", "ანანიაშვილი")]
    [InlineData("ანა", "ანას")]
    public void ContainsName_DoesNotMatchOtherWords(string name, string text)
    {
        Assert.False(NameMatcher.ContainsName(LatinSkeleton.Tokens(text), name));
    }

    [Fact]
    public void ContainsName_ShortNameMatchesOnlyExactly()
    {
        Assert.True(NameMatcher.ContainsName(LatinSkeleton.Tokens("ანა ბერიძე"), "ანა"));
    }

    [Fact]
    public void ContainsName_EveryWordOfAComposedNameMustBeFound()
    {
        Assert.True(NameMatcher.ContainsName(LatinSkeleton.Tokens("მარიასა და ანა"), "ანა-მარია"));
        Assert.False(NameMatcher.ContainsName(LatinSkeleton.Tokens("მარიას"), "ანა-მარია"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-")]
    public void ContainsName_EmptyName_IsNeverFound(string? name)
    {
        Assert.False(NameMatcher.ContainsName(LatinSkeleton.Tokens("ბერიძე"), name));
        Assert.False(NameMatcher.ContainsExactName(LatinSkeleton.Tokens("ბერიძე"), name));
    }

    [Fact]
    public void ContainsExactName_IgnoresCaseFormsAndSpelling()
    {
        Assert.True(NameMatcher.ContainsExactName(LatinSkeleton.Tokens("BERIDZE TAMAR"), "ბერიძე"));
        Assert.False(NameMatcher.ContainsExactName(LatinSkeleton.Tokens("ბერიძის"), "ბერიძე"));
    }
}
