using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     კონტრაქტის ნომრები დანიშნულების ტექსტში (D165). ნომრის ფორმატია "პ.ააა" (ContractNumbers: წლის ბოლო ციფრი და
///     სამნიშნა რიგითი). ემთხვევა მხოლოდ მთლიანი ნომერი, რომელსაც ციფრი, წერტილი ან მძიმე არ უსწრებს და ციფრი ან
///     ათწილადი არ მოსდევს ("16.150", "6.1500", "06.11.2026", "150.00" არა); "6-150" და "6/150" იგივეა, რაც "6.150"
/// </summary>
public static partial class ContractNumberFinder
{
    private const int RegexTimeoutMilliseconds = 1000;

    public static List<string> Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return
        [
            .. ContractNumberRegex().Matches(text)
                .Select(m => string.Create(CultureInfo.InvariantCulture, $"{m.Groups[1].Value}.{m.Groups[2].Value}"))
                .Distinct()
        ];
    }

    [GeneratedRegex(@"(?<![\d.,])(\d)[./\-](\d{3})(?!\d|[.,]\d)", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex ContractNumberRegex();
}
