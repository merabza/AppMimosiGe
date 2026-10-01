using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     გადახდის სასურველი დღის (StudentContracts.DesiredMonthlyPaymentDay) შემდეგი და მომდევნო გადახდის თარიღები
///     (Access-ის vStudentMustPayOnDesiredDate)
/// </summary>
public sealed record DesiredPayDates(DateTime NextPayDate, DateTime AfterNextPayDate)
{
    /// <summary>
    ///     Access-ის ფორმულები, D = სასურველი დღე:
    ///     NextPayDate = DateAdd("m", (Sgn(Day(Date()) - D) + 1) / 2, DateSerial(Year(Date()), Month(Date()), D)),
    ///     AfterNextPayDate = DateAdd("d", 1, DateAdd("m", (Sgn(Day(Date()) - D) + 1) / 2 + 1, DateSerial(...))).
    ///     DateSerial თვის დღეების გადაჭარბებას შემდეგ თვეში გადაიტანს (D = 31 სექტემბერში 1 ოქტომბერია). თვეების
    ///     რაოდენობა 0, 0.5 ან 1-ია და DateAdd წილადს ჭრის (Access-ის ასლზე შემოწმდა: 0.5 → 0, 1.5 → 1), ამიტომ D-ს დღეს
    ///     შემდეგი გადახდა ჯერ კიდევ დღესაა. DateAdd("m") თვის ბოლო დღეზე ჩერდება (31 იანვარი + 1 თვე = 28 თებერვალი),
    ///     როგორც AddMonths
    /// </summary>
    public static DesiredPayDates Calculate(DateTime today, int desiredDay)
    {
        DateTime dayInMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, today.Kind).AddDays(desiredDay - 1);
        int months = today.Day > desiredDay ? 1 : 0;
        return new DesiredPayDates(dayInMonth.AddMonths(months), dayInMonth.AddMonths(months + 1).AddDays(1));
    }
}
