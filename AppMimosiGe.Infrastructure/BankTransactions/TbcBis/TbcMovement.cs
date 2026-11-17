using System;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

internal sealed record TbcMovement(
    string? MovementId,
    decimal Amount,
    string? Currency,
    string? Purpose,
    bool IsDebit,
    DateTime? DocumentDate,
    string? AdditionalInformation,
    TbcPartnerAccountInfo? PartnerAccountInfo,
    int StatusCode,
    DateTime Date,
    string? AdditionalDescription,
    string? DocumentNumber,
    int TransactionType);
