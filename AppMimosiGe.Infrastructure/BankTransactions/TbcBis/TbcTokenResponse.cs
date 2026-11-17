using System.Text.Json.Serialization;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

//თიბისის BIS-ის JSON პასუხები (https://developers.tbcbank.ge/docs/bis-get-account-movements); მხოლოდ საჭირო ველები

internal sealed record TbcTokenResponse(
    [property: JsonPropertyName("access_token")]
    string? AccessToken,
    [property: JsonPropertyName("expires_in")]
    long ExpiresIn);

//application/problem+json
