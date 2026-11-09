using System;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

/// <summary>
///     თიბისის Business Integration Services-ის პარამეტრები (კონფიგურაციის სექცია "TbcBis"). ClientId და ClientSecret
///     Business Internet Banking-ის "API Integration"-იდანაა, მხოლოდ ისტორიის ნახვის უფლებით; ლოკალურად user secrets-შია,
///     production-ზე სერვერის appsettings.json-ში
/// </summary>
public sealed class TbcBisSettings
{
    public const string SectionName = "TbcBis";

    //სატესტო: https://test-api.tbcbank.ge, საბრძოლო: https://api.tbcbank.ge
    public string? BaseUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    //ორგანიზაციის ანგარიში (IBAN), რომლის ჩარიცხვებიც იკითხება
    public string? AccountIban { get; set; }

    public string Currency { get; set; } = "GEL";

    //ამ ანგარიშის BankAccounts-ის ჩანაწერი (თიბისი: 4), რომელზეც გადახდები იწერება
    public int? BankAccountId { get; set; }

    //გადასვლის დღე (D167): ჩარიცხვები ამ დღიდან მოდის, ამ ანგარიშზე ხელით შეყვანა ამ დღიდან აიკრძალება
    public DateTime? StartDate { get; set; }
}
