using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

//თიბისის BIS-ის JSON პასუხები (https://developers.tbcbank.ge/docs/bis-get-account-movements); მხოლოდ საჭირო ველები

internal sealed record TbcTokenResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("expires_in")] long ExpiresIn);

internal sealed record TbcMovementsResponse(List<TbcMovement>? Transactions, int Total);

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

internal sealed record TbcPartnerAccountInfo(
    string? AccountIban,
    string? BankCode,
    string? PartnerName,
    string? PersonalNumber,
    string? TaxCode);

//application/problem+json
internal sealed record TbcProblem(int Status, string? Code, string? Title);
