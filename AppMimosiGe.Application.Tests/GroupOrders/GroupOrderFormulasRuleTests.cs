using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ახალი წესები D0-დან (D187, D194): საათებისა და ფასის ფორმულა დღეებში, როცა მოსწავლის სტრიქონიც და განრიგიც
//მოქმედებს
public sealed class GroupOrderFormulasRuleTests
{
    private static readonly DateTime D0 = Day(10, 1);

    public static TheoryData<decimal, bool> Fees => new()
    {
        { 48m, false },
        { 48.01m, false },
        { 47.99m, false },
        { 48.0101m, true },
        { 50m, true }
    };

    public static TheoryData<decimal, decimal, float, string> FeeMismatches => new()
    {
        { 50m, 6m, 8f, "4 კვირის გადასახადი 50, უნდა იყოს 6 × 8 = 48" },
        //ფასი 2-ზე მეტი ათწილადით (D58): 16.6667 × 12 = 200.0004
        { 210m, 16.6667m, 12f, "4 კვირის გადასახადი 210, უნდა იყოს 16.67 × 12 = 200" }
    };

    //მოსწავლე საათის ფასით 6
    private static GroupStudentRow Student(float fourWeekHours, decimal fourWeekFee, DateTime startDate,
        DateTime? endDate = null, int lineId = 2, float hoursCoefficient = 1f, int studentContractId = 20)
    {
        return new GroupStudentRow(lineId, studentContractId, fourWeekHours, fourWeekFee, 6m, hoursCoefficient, null,
            startDate, endDate);
    }

    private static GroupDayTimePlaceRow Schedule(float hoursCount, DateTime startDate, DateTime? endDate = null,
        int lineId = 3)
    {
        return new GroupDayTimePlaceRow(lineId, 1, 4, hoursCount, 1, startDate, endDate);
    }

    private static IReadOnlyList<GroupOrderFormulaViolation> Find(GroupStudentRow[] students,
        GroupDayTimePlaceRow[] dayTimePlaces, DateTime? transitionDate = null)
    {
        return GroupOrderFormulasRule.Find(State(students: students, dayTimePlaces: dayTimePlaces),
            transitionDate ?? D0);
    }

    private static GroupOrderFormulaViolation Violation(EGroupOrderFormula formula, GroupStudentRow row,
        DateTime date, float weeklyHours)
    {
        return new GroupOrderFormulaViolation(formula, Fields.GroupCode, row, date, weeklyHours);
    }

    //დარღვევის აღწერა ტესტისთვის: სტრიქონის ხაზი
    private static string Line(GroupOrderFormulaViolation violation)
    {
        return violation.Row.LineId.ToString(CultureInfo.InvariantCulture);
    }

    //01.09-იდან განრიგი 2 საათია, მოსწავლეს კი 8 საათი (4 × 1 × 2) და 48 ლარი (6 × 8) აქვს
    [Fact]
    public void Find_RowsMatchTheFormulas_FindsNothing()
    {
        Assert.Empty(GroupOrderFormulasRule.Find(State(), D0));
    }

    //D194: D0-მდე ისტორია არ მოწმდება. სექტემბერში განრიგი 3 საათი იყო (ფორმულით 12), 01.10-იდან 2 (8): D0 = 01.10-ზე
    //დარღვევა არ ჩანს, D0 = 30.09-ზე კი ჩანს D0-ის დღიდან
    [Theory]
    [InlineData(9, 30, true)]
    [InlineData(10, 1, false)]
    public void Find_TheDaysBeforeD0_AreNotChecked(int month, int day, bool isFound)
    {
        DateTime[] expected = isFound ? [Day(month, day)] : [];

        IReadOnlyList<GroupOrderFormulaViolation> violations = Find([Student(8f, 48m, Day(9, 1))],
            [Schedule(3f, Day(9, 1), Day(10, 1)), Schedule(2f, Day(10, 1), lineId: 4)], Day(month, day));

        Assert.Equal(expected, violations.Select(v => v.Date));
    }

    //[StartDate, EndDate): D0-ს დასრულებული სტრიქონი D0-ს აღარ მოქმედებს, ერთი დღით გვიან დასრულებული კი D0-ს მოწმდება
    [Theory]
    [InlineData(10, 1, false)]
    [InlineData(10, 2, true)]
    public void Find_RowEndingOnD0_IsNotChecked(int month, int day, bool isFound)
    {
        DateTime[] expected = isFound ? [D0] : [];

        IReadOnlyList<GroupOrderFormulaViolation> violations =
            Find([Student(10f, 60m, Day(9, 1), Day(month, day))], [Schedule(2f, Day(9, 1))]);

        Assert.Equal(expected, violations.Select(v => v.Date));
    }

    //D0-ის შემდეგ დაწყებული სტრიქონი თავისი დაწყების დღიდან მოწმდება
    [Fact]
    public void Find_RowStartingAfterD0_IsCheckedFromItsStart()
    {
        GroupStudentRow late = Student(10f, 60m, Day(11, 1), lineId: 5, studentContractId: 30);

        IReadOnlyList<GroupOrderFormulaViolation> violations =
            Find([Student(8f, 48m, Day(9, 1)), late], [Schedule(2f, Day(9, 1))]);

        Assert.Equal([Violation(EGroupOrderFormula.Hours, late, Day(11, 1), 2f)], violations);
    }

    //განრიგი 15.11–01.12 3 საათია, ანუ სტრიქონი მხოლოდ ამ პერიოდში ირღვევა: დარღვევა ერთია, პირველი დღით
    [Fact]
    public void Find_ScheduleChangesAfterD0_ReportsTheFirstViolatedDayOnce()
    {
        GroupStudentRow row = Student(8f, 48m, Day(9, 1));

        IReadOnlyList<GroupOrderFormulaViolation> violations = Find([row],
        [
            Schedule(2f, Day(9, 1), Day(11, 15)), Schedule(3f, Day(11, 15), Day(12, 1), 4),
            Schedule(2f, Day(12, 1), lineId: 5)
        ]);

        Assert.Equal([Violation(EGroupOrderFormula.Hours, row, Day(11, 15), 3f)], violations);
    }

    //კვირის საათები დღის ყველა მოქმედი განრიგის სტრიქონის ჯამია: 1 + 1 = 2, კოეფიციენტით 1.5 → 12 საათი; 01.11-იდან
    //მეორე სტრიქონი აღარ არის (1 → 6 საათი)
    [Fact]
    public void Find_WeeklyHours_SumTheActiveScheduleRows()
    {
        GroupStudentRow row = Student(12f, 72m, Day(9, 1), hoursCoefficient: 1.5f);

        IReadOnlyList<GroupOrderFormulaViolation> violations =
            Find([row], [Schedule(1f, Day(9, 1)), Schedule(1f, Day(9, 1), Day(11, 1), 4)]);

        Assert.Equal([Violation(EGroupOrderFormula.Hours, row, Day(11, 1), 1f)], violations);
    }

    //D194: დღეებში, როცა განრიგი არ მოქმედებს, ფორმულა არ მოწმდება: 01.11-იდან განრიგი აღარ არის, ამიტომ მაშინ
    //დაწყებული სტრიქონის არც საათები მოწმდება და არც ფასი
    [Fact]
    public void Find_DaysWithoutASchedule_AreNotChecked()
    {
        Assert.Empty(Find(
            [Student(8f, 48m, Day(9, 1), Day(11, 1)), Student(10f, 50m, Day(11, 1), lineId: 5, studentContractId: 30)],
            [Schedule(2f, Day(9, 1), Day(11, 1))]));
    }

    //განრიგი მხოლოდ 01.12-იდან იწყება: სტრიქონი მაშინ მოწმდება
    [Fact]
    public void Find_ScheduleStartsLater_IsCheckedFromTheScheduleStart()
    {
        GroupStudentRow row = Student(8f, 48m, Day(9, 1));

        Assert.Equal([Violation(EGroupOrderFormula.Hours, row, Day(12, 1), 3f)],
            Find([row], [Schedule(3f, Day(12, 1))]));
    }

    //ფასი სტრიქონის საათებით მოწმდება (6 × 8 = 48), ზღვრით 0.01: დარღვევა D0-ის დღიდან
    [Theory]
    [MemberData(nameof(Fees))]
    public void Find_Fee_IsComparedWithTheRowHours(decimal fourWeekFee, bool isFound)
    {
        GroupStudentRow row = Student(8f, fourWeekFee, Day(9, 1));
        GroupOrderFormulaViolation[] expected = isFound ? [Violation(EGroupOrderFormula.Fee, row, D0, 2f)] : [];

        Assert.Equal(expected, Find([row], [Schedule(2f, Day(9, 1))]));
    }

    //საათები ზღვრით ედრება (D153): 8.00005 ესადაგება, 8.001 აღარა
    [Theory]
    [InlineData(8.00005f, false)]
    [InlineData(8.001f, true)]
    public void Find_Hours_AreComparedWithinTheTolerance(float fourWeekHours, bool isFound)
    {
        EGroupOrderFormula[] expected = isFound ? [EGroupOrderFormula.Hours] : [];

        IReadOnlyList<GroupOrderFormulaViolation> violations =
            Find([Student(fourWeekHours, 48m, Day(9, 1))], [Schedule(2f, Day(9, 1))]);

        Assert.Equal(expected, violations.Select(v => v.Formula));
    }

    //ერთ სტრიქონს ორივე ფორმულა ერღვევა: 10 საათი (8-ის ნაცვლად) და 50 ლარი (6 × 10 = 60-ის ნაცვლად); საათები პირველია
    [Fact]
    public void Find_BothFormulas_HoursGoFirst()
    {
        GroupStudentRow row = Student(10f, 50m, Day(9, 1));

        Assert.Equal([Violation(EGroupOrderFormula.Hours, row, D0, 2f), Violation(EGroupOrderFormula.Fee, row, D0, 2f)],
            Find([row], [Schedule(2f, Day(9, 1))]));
    }

    //რიგი: დღე, მერე სტრიქონის ხაზი, თუმცა სტრიქონები სხვა რიგითაა
    [Fact]
    public void Find_SeveralRows_GoByDateThenLine()
    {
        GroupStudentRow late = Student(10f, 60m, Day(11, 1), lineId: 5);
        GroupStudentRow second = Student(10f, 60m, Day(9, 1), lineId: 7, studentContractId: 30);
        GroupStudentRow first = Student(8f, 50m, Day(9, 1), lineId: 6, studentContractId: 40);

        IReadOnlyList<GroupOrderFormulaViolation> violations = Find([late, second, first], [Schedule(2f, Day(9, 1))]);

        Assert.Equal(
        [
            Violation(EGroupOrderFormula.Fee, first, D0, 2f), Violation(EGroupOrderFormula.Hours, second, D0, 2f),
            Violation(EGroupOrderFormula.Hours, late, Day(11, 1), 2f)
        ], violations);
    }

    [Fact]
    public void Describe_Hours_ShowsTheFormula()
    {
        GroupOrderFormulaViolation violation = Violation(EGroupOrderFormula.Hours,
            Student(8f, 48m, Day(9, 1), hoursCoefficient: 1.5f), Day(12, 1), 1.5f);

        Assert.Equal("მოსწავლე — ჯგუფი 601, 01.12.2026-დან: 4 კვირაში 8 საათი, უნდა იყოს 4 × 1.5 × 1.5 = 9",
            violation.Describe("მოსწავლე"));
    }

    [Theory]
    [MemberData(nameof(FeeMismatches))]
    public void Describe_Fee_ShowsTheFormula(decimal fourWeekFee, decimal oneHourFee, float fourWeekHours,
        string mismatch)
    {
        GroupOrderFormulaViolation violation = Violation(EGroupOrderFormula.Fee,
            Student(fourWeekHours, fourWeekFee, Day(9, 1)) with { OneHourFee = oneHourFee }, D0, 2f);

        Assert.Equal($"მოსწავლე — ჯგუფი 601, 01.10.2026-დან: {mismatch}", violation.Describe("მოსწავლე"));
    }

    //შენახვის შეცდომა საათებისაა, თუ საათების დარღვევა ერთი მაინც არის; ფასის დარღვევები მაშინ არ ჩამოითვლება
    [Fact]
    public void ToError_HoursViolations_GiveTheHoursError()
    {
        GroupOrderFormulaViolation[] violations =
        [
            Violation(EGroupOrderFormula.Fee, Student(8f, 50m, Day(9, 1), lineId: 6), D0, 2f),
            Violation(EGroupOrderFormula.Hours, Student(10f, 60m, Day(9, 1), lineId: 7), D0, 2f)
        ];

        Assert.Equal(GroupOrderErrors.StudentHoursDoNotMatchSchedule(["7"]),
            GroupOrderFormulasRule.ToError(violations, Line));
    }

    [Fact]
    public void ToError_OnlyFeeViolations_GiveTheFeeError()
    {
        GroupOrderFormulaViolation[] violations =
        [
            Violation(EGroupOrderFormula.Fee, Student(8f, 50m, Day(9, 1), lineId: 6), D0, 2f),
            Violation(EGroupOrderFormula.Fee, Student(8f, 47m, Day(9, 1), lineId: 7), Day(11, 1), 2f)
        ];

        Assert.Equal(GroupOrderErrors.StudentFeeDoesNotMatchHours(["6", "7"]),
            GroupOrderFormulasRule.ToError(violations, Line));
    }

    [Fact]
    public void ToError_NoViolations_IsNull()
    {
        Assert.Null(GroupOrderFormulasRule.ToError([], Line));
    }
}
