namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     გადახდა "უიმედო ვალის" ანგარიშით (BankAccounts.DesperateDebt): კონტრაქტის მოსწავლე, გადამხდელი და თანხა
/// </summary>
public sealed record DebtPayment(int PaymentId, int StudentHumanId, int PayerHumanId, decimal Amount);
