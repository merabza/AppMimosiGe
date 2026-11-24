using AppMimosiGe.Application.GroupOrders.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//საათებისა და ფასის ფორმულები (D187)
public sealed class GroupOrderFormulasTests
{
    //[StartDate, EndDate): ითვლება მხოლოდ d დღეს მოქმედი სტრიქონები; დასრულების დღეს სტრიქონი აღარ ითვლება
    [Theory]
    [InlineData(9, 30, 3f)]
    [InlineData(10, 1, 2.5f)]
    [InlineData(10, 2, 4.5f)]
    public void WeeklyHours_SumsTheRowsActiveOnTheDay(int month, int day, float weeklyHours)
    {
        GroupDayTimePlaceRow[] rows =
        [
            new(1, 1, 4, 1.5f, 1, Day(9, 1), null), new(2, 3, 4, 1.5f, 1, Day(9, 1), Day(10, 1)),
            new(3, 4, 4, 1f, 1, Day(10, 1), null), new(4, 5, 4, 2f, 1, Day(10, 2), null)
        ];

        Assert.Equal(weeklyHours, GroupOrderFormulas.WeeklyHours(rows, Day(month, day)));
    }

    [Fact]
    public void WeeklyHours_NoActiveRow_IsZero()
    {
        Assert.Equal(0f, GroupOrderFormulas.WeeklyHours([DayTimePlaceRow(Day(10, 1))], Day(9, 30)));
    }

    //4 × კოეფიციენტი × კვირის საათები: 1.5-საათიანი სტრიქონები და კოეფიციენტი ≠ 1
    [Theory]
    [InlineData(1f, 2f, 8f)]
    [InlineData(1f, 1.5f, 6f)]
    [InlineData(1f, 3f, 12f)]
    [InlineData(1f, 4.5f, 18f)]
    [InlineData(1.5f, 3f, 18f)]
    [InlineData(1.5f, 1.5f, 9f)]
    [InlineData(0.5f, 10f, 20f)]
    public void FourWeekHours_IsFourTimesTheCoefficientTimesTheWeeklyHours(float hoursCoefficient, float weeklyHours,
        float fourWeekHours)
    {
        Assert.Equal(fourWeekHours, GroupOrderFormulas.FourWeekHours(hoursCoefficient, weeklyHours));
    }

    //საათები ზღვრით ედრება (GeneratorHours, D153): 0.00005-იანი სხვაობა ფორმულას ესადაგება, 0.001-იანი აღარა
    [Theory]
    [InlineData(12f, 1f, 3f, true)]
    [InlineData(12.00005f, 1f, 3f, true)]
    [InlineData(11.99995f, 1f, 3f, true)]
    [InlineData(12.001f, 1f, 3f, false)]
    [InlineData(8f, 1f, 3f, false)]
    [InlineData(18f, 1.5f, 3f, true)]
    [InlineData(12f, 1.5f, 3f, false)]
    public void HoursMatchSchedule_ComparesWithTheFormulaWithinTheTolerance(float fourWeekHours, float hoursCoefficient,
        float weeklyHours, bool matches)
    {
        Assert.Equal(matches, GroupOrderFormulas.HoursMatchSchedule(fourWeekHours, hoursCoefficient, weeklyHours));
    }

    //საათის ფასი × საათები; float-ის საათები 7 ციფრამდე მრგვალდება (≈ 6.6 → 6.6), როგორც FeeMatchesHours-ში
    [Fact]
    public void FourWeekFee_IsTheHourFeeTimesTheHours()
    {
        Assert.Equal(72m, GroupOrderFormulas.FourWeekFee(6m, 12f));
        Assert.Equal(66m, GroupOrderFormulas.FourWeekFee(10m, GroupOrderFormulas.FourWeekHours(1.1f, 1.5f)));
    }

    public static TheoryData<decimal, decimal, float, bool> FeeCases => new()
    {
        { 72m, 6m, 12f, true },
        //ზუსტად 0.01 ჯერ კიდევ ესადაგება, როგორც r30-ში (Abs(…) > 0.01)
        { 72.01m, 6m, 12f, true },
        { 71.99m, 6m, 12f, true },
        { 72.0101m, 6m, 12f, false },
        { 71.9899m, 6m, 12f, false },
        { 48m, 6m, 12f, false },
        //ფასი 2-ზე მეტი ათწილადით (D58): 16.6667 × 12 = 200.0004
        { 200m, 16.6667m, 12f, true },
        //1.5 საათი კვირაში → 6
        { 36m, 6m, 6f, true }
    };

    //|4 კვირის გადასახადი − საათის ფასი × 4 კვირის საათები| ≤ 0.01
    [Theory]
    [MemberData(nameof(FeeCases))]
    public void FeeMatchesHours_AllowsADifferenceUpToOneTetri(decimal fourWeekFee, decimal oneHourFee,
        float fourWeekHours, bool matches)
    {
        Assert.Equal(matches, GroupOrderFormulas.FeeMatchesHours(fourWeekFee, oneHourFee, fourWeekHours));
    }

    public static TheoryData<decimal, bool> FeesForSixPointSixHours => new()
    {
        { 66m, true },
        { 65.99m, true },
        { 66.01m, true },
        { 65.9899m, false },
        { 66.0101m, false }
    };

    //კოეფიციენტი 1.1 და 1.5 საათი: float-ის საათები (≈ 6.6) 7 ციფრამდე მრგვალდება, როგორც r30-ში, ამიტომ ზუსტად
    //0.01-იანი სხვაობა ზღვრის ორივე მხარეს ესადაგება
    [Theory]
    [MemberData(nameof(FeesForSixPointSixHours))]
    public void FeeMatchesHours_CoefficientOtherThanOne_UsesTheIntendedDecimalHours(decimal fourWeekFee, bool matches)
    {
        float fourWeekHours = GroupOrderFormulas.FourWeekHours(1.1f, 1.5f);

        Assert.Equal(matches, GroupOrderFormulas.FeeMatchesHours(fourWeekFee, 10m, fourWeekHours));
    }
}
