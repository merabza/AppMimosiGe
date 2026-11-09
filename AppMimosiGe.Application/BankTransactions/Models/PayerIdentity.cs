using System;
using System.Linq;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ჩარიცხვის გადამხდელის იდენტიფიკატორები: პირადი ნომერი (taxCode, თუ 11 ციფრია, თორემ personalNumber) და IBAN
///     (ჰარეების გარეშე, დიდი ასოებით)
/// </summary>
public static class PayerIdentity
{
    private const int PersonalIdLength = 11;

    public static string? PersonalNumber(string? taxCode, string? personalNumber)
    {
        return PersonalId(taxCode) ?? PersonalId(personalNumber);
    }

    //საქართველოს მოქალაქის პირადი ნომერი: 11 ციფრი (კომპანიის საიდენტიფიკაციო კოდი 9-ია)
    public static string? PersonalId(string? value)
    {
        string? trimmed = value?.Trim();
        return trimmed is { Length: PersonalIdLength } && trimmed.All(char.IsAsciiDigit) ? trimmed : null;
    }

    public static string? Iban(string? value)
    {
        string? normalized = value?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
