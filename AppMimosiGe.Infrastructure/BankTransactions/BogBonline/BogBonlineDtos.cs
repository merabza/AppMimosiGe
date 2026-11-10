using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

//საქართველოს ბანკის Business Online-ის JSON პასუხები (https://api.bog.ge/docs/en/bonline/statement/statementV2);
//მხოლოდ საჭირო ველები. თარიღებს +04:00 ახლავს, ამიტომ DateTimeOffset-ად იკითხება

internal sealed record BogTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] long ExpiresIn);

internal sealed record BogStatementResponse(long Id, int Count, int TotalCount, List<BogStatementRecord>? Records);

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

internal sealed record BogPartyDetails(string? Name, string? Inn, string? AccountNumber, string? BankCode);

//შეცდომის პასუხი: Keycloak-ის ტოკენი (error, error_description) ან API (Message); სხვა ფორმას მხოლოდ HTTP სტატუსი აქვს
internal sealed record BogError(
    string? Message,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription);
