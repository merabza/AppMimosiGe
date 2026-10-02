using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Salary.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Salary;

public sealed class SalaryCalculatorTests
{
    private const int SchemeEight = 11;
    private const int SchemeTen = 16;
    private const int AdditionType = 3;
    private const int DeductionType = 4;
    private const int NotCountedType = 99;

    private static readonly DateTime ChargeDate = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly HashSet<DateTime> AllMonths =
    [
        new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified),
        new(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified)
    ];

    private static readonly Dictionary<int, decimal> Rates = new() { [SchemeEight] = 8m, [SchemeTen] = 10m };

    private static readonly Dictionary<int, int?> CountPlaces = new()
    {
        [1] = 1, [AdditionType] = 1, [DeductionType] = 2, [NotCountedType] = null
    };

    private static DateTime Day(int month, int day, int hour = 10)
    {
        return new DateTime(2026, month, day, hour, 0, 0, DateTimeKind.Unspecified);
    }

    private static SalaryLessonStudentRow Row(int lessonId, int teacher, DateTime lessonDt, float hours,
        int groupId = 100, int? substitute = null, int scheme = SchemeEight, int status = 1)
    {
        return new SalaryLessonStudentRow(lessonId, groupId, teacher, substitute, scheme, lessonDt, status, hours);
    }

    private static SalaryContractData Contract(int id, bool pension = true, bool indEnt = false,
        bool nextMonth = false)
    {
        return new SalaryContractData(id, pension, indEnt, nextMonth);
    }

    private static SalaryCalculationResult Calculate(IReadOnlyList<SalaryContractData> contracts,
        IReadOnlyList<SalaryLessonStudentRow> rows, IReadOnlyList<SalaryPartData>? parts = null,
        IReadOnlySet<DateTime>? months = null)
    {
        return SalaryCalculator.Calculate(new SalaryCalculationInput(ChargeDate, contracts, rows, months ?? AllMonths,
            Rates, CountPlaces, parts ?? []));
    }

    private static decimal LessonAmount(SalaryCalculationResult result, int teacher)
    {
        return Assert.Single(result.LessonParts, x => x.TeacherContractId == teacher).Amount;
    }

    // --- ჩატარებული გაკვეთილების ხელფასი (ტიპი 1) ---

    [Fact]
    public void Calculate_EachLessonCountsTheMaximumHoursOfItsStudents()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1)],
            [Row(1, 1, Day(9, 10), 1.5f), Row(1, 1, Day(9, 10), 2f), Row(2, 1, Day(9, 17), 1.5f)]);

        // Assert: (2 + 1.5) × 8
        Assert.Equal(28m, LessonAmount(result, 1));
    }

    [Fact]
    public void Calculate_SubstituteTeacherGetsTheLesson()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1), Contract(2)],
            [Row(1, 1, Day(9, 10), 2f), Row(2, 1, Day(9, 17), 2f, substitute: 2)]);

        // Assert
        Assert.Equal(16m, LessonAmount(result, 1));
        Assert.Equal(16m, LessonAmount(result, 2));
    }

    [Fact]
    public void Calculate_CancelledLessonsAreNotPaid()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1)],
            [Row(1, 1, Day(9, 10), 2f), Row(2, 1, Day(9, 17), 2f, status: 2), Row(3, 1, Day(9, 24), 1f, status: 3)]);

        // Assert: სტატუსი 3 (არა 2) ითვლება
        Assert.Equal(24m, LessonAmount(result, 1));
    }

    [Fact]
    public void Calculate_LessonsOfOtherMonthsAndMonthsMissingFromOperationMonthsAreNotPaid()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1)],
            [Row(1, 1, Day(9, 30, 18), 2f), Row(2, 1, Day(10, 1, 9), 2f), Row(3, 1, Day(8, 31), 2f)]);
        SalaryCalculationResult withoutSeptember = Calculate([Contract(1)], [Row(1, 1, Day(9, 30), 2f)],
            months: new HashSet<DateTime> { new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified) });

        // Assert
        Assert.Equal(16m, LessonAmount(result, 1));
        Assert.Empty(withoutSeptember.LessonParts);
    }

    [Fact]
    public void Calculate_HoursOfEachSchemeArePaidByTheirRate()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1)],
            [Row(1, 1, Day(9, 10), 2f), Row(2, 1, Day(9, 11), 1.5f, 200, scheme: SchemeTen)]);

        // Assert: 2 × 8 + 1.5 × 10
        Assert.Equal(31m, LessonAmount(result, 1));
    }

    [Fact]
    public void Calculate_NextMonthContractIsPaidForTheLessonsOfTheChargeMonth()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(3, nextMonth: true)],
            [Row(1, 3, Day(9, 10), 2f), Row(2, 3, Day(10, 2), 1f)]);

        // Assert: 2026-10-05 = ოქტომბრის 1-ლი + 4 დღე
        Assert.Equal(8m, LessonAmount(result, 3));
    }

    [Fact]
    public void Calculate_ChargeDateOtherThanTheFifthGetsNoLessonParts()
    {
        // Act
        SalaryCalculationResult result = SalaryCalculator.Calculate(new SalaryCalculationInput(
            new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Unspecified), [Contract(1)], [Row(1, 1, Day(9, 10), 2f)],
            AllMonths, Rates, CountPlaces, []));

        // Assert
        Assert.Empty(result.LessonParts);
    }

    [Fact]
    public void Calculate_LessonPartsAreOrderedByContract()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(2), Contract(1)],
            [Row(1, 2, Day(9, 10), 1f), Row(2, 1, Day(9, 10), 1f)]);

        // Assert
        Assert.Equal([1, 2], result.LessonParts.Select(x => x.TeacherContractId));
    }

    [Theory]
    [InlineData(9, false, "2026-10-05")]
    [InlineData(9, true, "2026-09-05")]
    [InlineData(12, false, "2027-01-05")]
    public void SalaryDate_IsTheNextOrTheSameMonthsFifth(int month, bool nextMonth, string expected)
    {
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            SalaryCalculator.SalaryDate(new DateTime(2026, month, 1, 0, 0, 0, DateTimeKind.Unspecified), nextMonth));
    }

    // --- დამრგვალება ---

    [Theory]
    [InlineData(133.2, 134)]
    [InlineData(134, 134)]
    [InlineData(135, 136)]
    [InlineData(0.01, 2)]
    [InlineData(0, 0)]
    [InlineData(-5, -4)]
    public void RoundUpToTwo_RoundsUpToAMultipleOfTwo(decimal sum, decimal expected)
    {
        Assert.Equal(expected, SalaryCalculator.RoundUpToTwo(sum));
    }

    [Theory]
    [InlineData(10.01, 10.04)]
    [InlineData(10.04, 10.04)]
    [InlineData(10.0401, 10.08)]
    [InlineData(0.001, 0.04)]
    [InlineData(0, 0)]
    [InlineData(-0.05, -0.04)]
    public void RoundUpToFourHundredths_RoundsUpToAMultipleOfFourHundredths(decimal sum, decimal expected)
    {
        Assert.Equal(expected, SalaryCalculator.RoundUpToFourHundredths(sum));
    }

    [Theory]
    [InlineData(1.00005, 1.0000)]
    [InlineData(1.00015, 1.0002)]
    [InlineData(2.123456, 2.1235)]
    public void Money_RoundsToFourDecimalsHalfToEven(decimal value, decimal expected)
    {
        Assert.Equal(expected, SalaryCalculator.Money(value));
    }

    // --- სტრიქონები ---

    [Fact]
    public void Calculate_PensionSchemeLine()
    {
        // Act: 99.5 → 100
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1)], [],
            [new SalaryPartData(1, AdditionType, 99.5m), new SalaryPartData(1, DeductionType, 10m)]).Lines);

        // Assert
        Assert.Equal(1, line.TeacherContractId);
        Assert.Equal(100m, line.NetAmountRound);
        Assert.Equal(125m, line.AmountGross);
        Assert.Equal(2.5m, line.Pension2);
        Assert.Equal(122.5m, line.GrossMinusPension);
        Assert.Equal(24.5m, line.IncomeTax);
        Assert.Equal(10m, line.Gamokvitva);
        Assert.Equal(5m, line.Pension4);
        Assert.Equal(88m, line.AmountNet);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), line.MonthDate);
        Assert.Equal(1, line.RsQuoteTypeId);
        Assert.Equal(0m, line.IndividualIncomeTax);
    }

    [Fact]
    public void Calculate_NoPensionSchemeLine()
    {
        // Act: 100.01 → 100.04
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1, false)], [],
            [new SalaryPartData(1, AdditionType, 100.01m), new SalaryPartData(1, DeductionType, 0.04m)]).Lines);

        // Assert
        Assert.Equal(100.04m, line.NetAmountRound);
        Assert.Equal(125.05m, line.AmountGross);
        Assert.Equal(0m, line.Pension2);
        Assert.Equal(125.05m, line.GrossMinusPension);
        Assert.Equal(25.01m, line.IncomeTax);
        Assert.Equal(0.04m, line.Gamokvitva);
        Assert.Equal(0m, line.Pension4);
        Assert.Equal(100m, line.AmountNet);
        Assert.Equal(1, line.RsQuoteTypeId);
        Assert.Equal(0m, line.IndividualIncomeTax);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Calculate_IndividualEntrepreneurLine(bool pension)
    {
        // Act: 99.99 → 100 (ინდ. მეწარმე საპენსიოს მიუხედავად 0.04-მდე მრგვალდება)
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1, pension, true)], [],
            [new SalaryPartData(1, AdditionType, 99.99m), new SalaryPartData(1, DeductionType, 5m)]).Lines);

        // Assert
        Assert.Equal(100m, line.NetAmountRound);
        Assert.Equal(100m, line.AmountGross);
        Assert.Equal(0m, line.Pension2);
        Assert.Equal(100m, line.GrossMinusPension);
        Assert.Equal(0m, line.IncomeTax);
        Assert.Equal(5m, line.Gamokvitva);
        Assert.Equal(0m, line.Pension4);
        Assert.Equal(94m, line.AmountNet);
        Assert.Equal(6, line.RsQuoteTypeId);
        Assert.Equal(1m, line.IndividualIncomeTax);
    }

    [Fact]
    public void Calculate_LessonPartsAreAddedToTheManualPartsOfTheLine()
    {
        // Act: 16 (გაკვეთილები) + 3 = 19 → 20
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1)], [Row(1, 1, Day(9, 10), 2f)],
            [new SalaryPartData(1, AdditionType, 3m)]).Lines);

        // Assert
        Assert.Equal(20m, line.NetAmountRound);
    }

    [Fact]
    public void Calculate_PartsWithoutTypeOrCountPlaceAreNotCounted()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1)], [],
            [new SalaryPartData(1, null, 50m), new SalaryPartData(1, NotCountedType, 50m),
                new SalaryPartData(1, 1234, 50m)]);

        // Assert
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Calculate_LineNeedsAnAdditionOrADeductionOfAtLeastOneTetri()
    {
        // Act
        SalaryCalculationResult result = Calculate(
            [Contract(1), Contract(2), Contract(3, false), Contract(4), Contract(5)], [],
            [
                new SalaryPartData(2, DeductionType, 0.01m), new SalaryPartData(3, AdditionType, 0.001m),
                new SalaryPartData(4, DeductionType, 0.0099m), new SalaryPartData(5, AdditionType, 0m)
            ]);

        // Assert: 2 გამოქვითვით, 3 დამრგვალებით (0.04); 1, 4 და 5 არა
        Assert.Equal([2, 3], result.Lines.Select(x => x.TeacherContractId));
        CalculatedSalaryLine deductionOnly = result.Lines[0];
        Assert.Equal(0m, deductionOnly.NetAmountRound);
        Assert.Equal(-0.01m, deductionOnly.AmountNet);
        Assert.Equal(0.04m, result.Lines[1].NetAmountRound);
    }

    [Fact]
    public void Calculate_PartsOfOtherContractsAreNotMixed()
    {
        // Act
        SalaryCalculationResult result = Calculate([Contract(1), Contract(2)], [],
            [new SalaryPartData(1, AdditionType, 10m), new SalaryPartData(2, AdditionType, 20m)]);

        // Assert
        Assert.Equal([10m, 20m], result.Lines.Select(x => x.NetAmountRound));
    }

    // --- დეტალები ---

    [Fact]
    public void Calculate_DetailsAreTheGroupsOfTheLinesMonth()
    {
        // Act: ჯგუფი 100 ორი სქემით, ჯგუფი 200
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1)],
        [
            Row(1, 1, Day(9, 10), 2f), Row(2, 1, Day(9, 11), 1f, scheme: SchemeTen), Row(3, 1, Day(9, 12), 1.5f, 200),
            Row(4, 1, Day(10, 1), 2f, 300)
        ]).Lines);

        // Assert: ოქტომბრის ჯგუფი 300 სხვა უწყისისაა
        Assert.Equal(
        [
            new CalculatedSalaryLineDetail(100, 26m, 3f, 8.6667m), new CalculatedSalaryLineDetail(200, 12m, 1.5f, 8m)
        ], line.Details);
    }

    [Fact]
    public void Calculate_NextMonthContractDetailsAreTheGroupsOfThePaidMonth()
    {
        // Act
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(3, nextMonth: true)],
            [Row(1, 3, Day(9, 10), 2f), Row(2, 3, Day(10, 2), 1f, 200)]).Lines);

        // Assert
        Assert.Equal([new CalculatedSalaryLineDetail(200, 8m, 1f, 8m)], line.Details);
    }

    [Fact]
    public void Calculate_GroupWithoutHoursHasZeroHourCost()
    {
        // Act
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1)], [Row(1, 1, Day(9, 10), 0f)],
            [new SalaryPartData(1, AdditionType, 10m)]).Lines);

        // Assert
        Assert.Equal([new CalculatedSalaryLineDetail(100, 0m, 0f, 0m)], line.Details);
    }

    [Fact]
    public void Calculate_LineWithoutLessonsHasNoDetails()
    {
        // Act
        CalculatedSalaryLine line = Assert.Single(Calculate([Contract(1)], [],
            [new SalaryPartData(1, AdditionType, 10m)]).Lines);

        // Assert
        Assert.Empty(line.Details);
    }
}
