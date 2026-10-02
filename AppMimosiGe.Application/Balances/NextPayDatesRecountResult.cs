namespace AppMimosiGe.Application.Balances;

/// <summary>
///     გადათვლილი კონტრაქტები და მათგან, რომელთა შემდეგი გადახდის თარიღი შეიცვალა
/// </summary>
public sealed record NextPayDatesRecountResult(int StudentContractsCount, int ChangedCount);
