namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

internal sealed record TbcProblem(int Status, string? Code, string? Title);
