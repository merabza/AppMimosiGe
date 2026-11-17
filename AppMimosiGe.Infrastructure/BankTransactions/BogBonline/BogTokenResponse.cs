using System.Text.Json.Serialization;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

//საქართველოს ბანკის Business Online-ის JSON პასუხები (https://api.bog.ge/docs/en/bonline/statement/statementV2);
//მხოლოდ საჭირო ველები. თარიღებს +04:00 ახლავს, ამიტომ DateTimeOffset-ად იკითხება

internal sealed record BogTokenResponse(
    [property: JsonPropertyName("access_token")]
    string? AccessToken,
    [property: JsonPropertyName("expires_in")]
    long ExpiresIn);

//შეცდომის პასუხი: Keycloak-ის ტოკენი (error, error_description) ან API (Message); სხვა ფორმას მხოლოდ HTTP სტატუსი აქვს
