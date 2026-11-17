namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

internal sealed record TbcPartnerAccountInfo(
    string? AccountIban,
    string? BankCode,
    string? PartnerName,
    string? PersonalNumber,
    string? TaxCode);
