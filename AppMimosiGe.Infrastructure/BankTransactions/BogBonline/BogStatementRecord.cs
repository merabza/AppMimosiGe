using System;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

internal sealed record BogStatementRecord(
    decimal? EntryId,
    DateTimeOffset? EntryDate,
    string? EntryDocumentNumber,
    decimal? EntryAmountDebit,
    decimal? EntryAmountCredit,
    DateTimeOffset? DocumentValueDate,
    string? DocumentNomination,
    string? DocumentInformation,
    string? DocumentPayerInn,
    BogPartyDetails? SenderDetails,
    BogPartyDetails? BeneficiaryDetails);
