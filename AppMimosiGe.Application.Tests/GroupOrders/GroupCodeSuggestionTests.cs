using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ჯგუფის კოდის შეთავაზება (D186): „{კლასი}{NN}", NN წელში პირველი თავისუფალი ორნიშნა ნომერია
public sealed class GroupCodeSuggestionTests
{
    //კლასი სტატუსის Rate-იდან: 101–112 → 1–12, 212 → 12, 200 → 13, 312 → 14
    [Theory]
    [InlineData(101, 1)]
    [InlineData(106, 6)]
    [InlineData(112, 12)]
    [InlineData(212, 12)]
    [InlineData(200, 13)]
    [InlineData(312, 14)]
    [InlineData(100, null)]
    [InlineData(113, null)]
    [InlineData(201, null)]
    [InlineData(211, null)]
    [InlineData(300, null)]
    public void SchoolClass_ComesFromTheStatusRate(int studentStatusRate, int? schoolClass)
    {
        Assert.Equal(schoolClass, GroupCodeSuggestion.SchoolClass(studentStatusRate));
    }

    //NN ორნიშნაა და 01-იდან იწყება
    [Theory]
    [InlineData(106, "601")]
    [InlineData(110, "1001")]
    [InlineData(212, "1201")]
    [InlineData(312, "1401")]
    public void Suggest_NoCodesTaken_GivesTheFirstNumber(int studentStatusRate, string code)
    {
        Assert.Equal(code, GroupCodeSuggestion.Suggest(studentStatusRate, []));
    }

    //ჯერ ხვრელი ივსება: 601 და 603 დაკავებულია, 602 თავისუფალია
    [Fact]
    public void Suggest_TakesTheFirstHole()
    {
        Assert.Equal("602", GroupCodeSuggestion.Suggest(106, ["603", "601", "604"]));
    }

    [Fact]
    public void Suggest_NoHole_GivesTheNumberAfterTheLast()
    {
        Assert.Equal("603", GroupCodeSuggestion.Suggest(106, ["601", "602"]));
    }

    //სხვა კლასების კოდები ნომერს არ იკავებს, თუმცა მათი ციფრებიც იწყება 6-ით ან 01-ით მთავრდება
    [Fact]
    public void Suggest_OtherClassesCodes_DoNotTakeTheNumber()
    {
        Assert.Equal("601", GroupCodeSuggestion.Suggest(106, ["701", "1001", "6", "6010", "60"]));
    }

    //დაკავებულია კოდი და არა ჯგუფის სტატუსი: Dev-ზე 1001 მე-4 კლასის სტატუსის ჯგუფს აქვს, მაგრამ მე-10 კლასს ნომერს
    //მაინც უკავებს; კოდი გამოტოვებებით ედრება
    [Fact]
    public void Suggest_TakenCodes_AreComparedAsCodes()
    {
        Assert.Equal("1002", GroupCodeSuggestion.Suggest(110, [" 1001 "]));
    }

    //01–99 ყველა დაკავებულია, ან სტატუსს კლასი არ აქვს: შეთავაზება არ არის
    [Fact]
    public void Suggest_NoFreeNumberOrNoClass_IsNull()
    {
        Assert.Null(GroupCodeSuggestion.Suggest(106,
            Enumerable.Range(1, 99).Select(n => string.Create(CultureInfo.InvariantCulture, $"6{n:00}"))));
        Assert.Null(GroupCodeSuggestion.Suggest(300, []));
    }
}
