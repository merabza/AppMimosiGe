using System;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

/// <summary>
///     საქართველოს ბანკის Business Online API-ს პარამეტრები (კონფიგურაციის სექცია "BogBonline"). ClientId და ClientSecret
///     bonline.bog.ge/admin/api-ზე დარეგისტრირებული აპლიკაციისაა (D172); ლოკალურად user secrets-შია, production-ზე
///     სერვერის appsettings.json-ში
/// </summary>
public sealed class BogBonlineSettings
{
    public const string SectionName = "BogBonline";

    //ტოკენის მისამართი (Keycloak, client credentials):
    //https://account.bog.ge/auth/realms/bog/protocol/openid-connect/token
    public string? TokenUrl { get; set; }

    //API-ს ბაზა (https://api.businessonline.ge/api); ფარდობითი მისამართები მას ემატება
    public string? BaseUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    //ორგანიზაციის ანგარიში (IBAN ვალუტის სუფიქსის გარეშე), რომლის ჩარიცხვებიც იკითხება
    public string? AccountIban { get; set; }

    public string Currency { get; set; } = "GEL";

    //ამ ანგარიშის BankAccounts-ის ჩანაწერი (საქართველოს ბანკი: 1, D174), რომელზეც გადახდები იწერება
    public int? BankAccountId { get; set; }

    //გადასვლის დღე (D167, D174): ჩარიცხვები ამ დღიდან მოდის, ამ ანგარიშზე ხელით შეყვანა ამ დღიდან აიკრძალება
    public DateTime? StartDate { get; set; }
}
