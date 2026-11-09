using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     სახელის ან გვარის პოვნა ტექსტის სიტყვებში (LatinSkeleton-ის ფორმით) ბრუნვის დაბოლოებების გათვალისწინებით:
///     "ბერიძის", "ბერიძისთვის", "ბერიძეს", "გელაშვილისათვის", "ნინოს". ფუძე = სახელი ბოლო ხმოვნის გარეშე; სიტყვა ემთხვევა,
///     თუ ფუძით იწყება და დარჩენილი ნაწილი (შესაძლოა ერთი ხმოვნით დაწყებული) ცნობილი დაბოლოებაა. 3 ასოზე მოკლე ფუძე
///     მხოლოდ ზუსტად ემთხვევა, რომ მოკლე სახელი ყველაფერს არ დაემთხვეს. რამდენსიტყვიანი სახელის (მაგ. "ანა-მარია")
///     ყველა სიტყვა უნდა მოიძებნოს
/// </summary>
public static class NameMatcher
{
    private const int MinStemLength = 3;
    private const string Vowels = "aeiou";

    //ბრუნვის ნიშნები და თანდებულები ფუძის (ან ფუძე + ხმოვნის) შემდეგ
    private static readonly HashSet<string> Endings =
    [
        "", "s", "sa", "t", "ta", "d", "ma", "m", "ze", "tan", "stan", "tvis", "stvis", "satvis", "gan", "sgan",
        "isgan", "eb", "ebi", "ebs", "ebis", "ebistvis", "ebze", "ebtan", "ebma"
    ];

    public static bool ContainsName(IReadOnlyCollection<string> tokens, string? name)
    {
        List<string> nameTokens = LatinSkeleton.Tokens(name);
        return nameTokens.Count > 0 && nameTokens.All(nameToken => tokens.Any(token => Matches(token, nameToken)));
    }

    //სიტყვა ზუსტად ემთხვევა სახელს (დაბოლოებების გარეშე: გადამხდელის სახელი ბანკში სახელობითშია)
    public static bool ContainsExactName(IReadOnlyCollection<string> tokens, string? name)
    {
        List<string> nameTokens = LatinSkeleton.Tokens(name);
        return nameTokens.Count > 0 && nameTokens.All(tokens.Contains);
    }

    public static bool Matches(string token, string nameToken)
    {
        if (token == nameToken)
        {
            return true;
        }

        string stem = Vowels.Contains(nameToken[^1], StringComparison.Ordinal) ? nameToken[..^1] : nameToken;
        if (stem.Length < MinStemLength || !token.StartsWith(stem, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = token[stem.Length..];
        return Endings.Contains(rest) ||
               rest.Length > 0 && Vowels.Contains(rest[0], StringComparison.Ordinal) && Endings.Contains(rest[1..]);
    }
}
