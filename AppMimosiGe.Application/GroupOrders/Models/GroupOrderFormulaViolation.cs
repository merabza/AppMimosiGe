using System;
using System.Globalization;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ფორმულის დარღვევა (GroupOrderFormulasRule): ჯგუფის კოდი, მოსწავლის სტრიქონი, D0-დან პირველი დღე, როცა ფორმულა
///     ირღვევა, და ამ დღის განრიგის კვირის საათები
/// </summary>
public sealed record GroupOrderFormulaViolation(
    EGroupOrderFormula Formula,
    string GroupCode,
    GroupStudentRow Row,
    DateTime Date,
    float WeeklyHours)
{
    //"მოსწავლე — ჯგუფი 601, 01.12.2026-დან: 4 კვირაში 8 საათი, უნდა იყოს 4 × 1 × 3 = 12";
    //"მოსწავლე — ჯგუფი 601, 01.12.2026-დან: 4 კვირის გადასახადი 50, უნდა იყოს 6 × 8 = 48"
    public string Describe(string studentName)
    {
        string mismatch = Formula == EGroupOrderFormula.Hours ? HoursMismatch() : FeeMismatch();
        return string.Create(CultureInfo.InvariantCulture,
            $"{studentName} — ჯგუფი {GroupCode}, {Date:dd.MM.yyyy}-დან: {mismatch}");
    }

    private string HoursMismatch()
    {
        float mustHours = GroupOrderFormulas.FourWeekHours(Row.HoursCoefficient, WeeklyHours);
        return string.Create(CultureInfo.InvariantCulture,
            $"4 კვირაში {Hours(Row.FourWeekHours)} საათი, უნდა იყოს 4 × {Hours(Row.HoursCoefficient)} × " +
            $"{Hours(WeeklyHours)} = {Hours(mustHours)}");
    }

    private string FeeMismatch()
    {
        decimal mustFee = GroupOrderFormulas.FourWeekFee(Row.OneHourFee, Row.FourWeekHours);
        return string.Create(CultureInfo.InvariantCulture,
            $"4 კვირის გადასახადი {Money(Row.FourWeekFee)}, უნდა იყოს {Money(Row.OneHourFee)} × " +
            $"{Hours(Row.FourWeekHours)} = {Money(mustFee)}");
    }

    private static string Hours(float hours)
    {
        return hours.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Money(decimal amount)
    {
        return amount.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
