using System.Text.Json.Serialization;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

internal sealed record BogError(
    string? Message,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("error_description")]
    string? ErrorDescription);
