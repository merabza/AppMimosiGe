using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ტექსტის „ლათინური ჩონჩხი“ სახელების შესადარებლად (TBC_INTEGRATION.md §4): ქართული ასოები ლათინურად (ეროვნული
///     ტრანსლიტერაციით), ხოლო ის ასოები, რომლებსაც ლათინურად სხვადასხვანაირად წერენ, ერთ ფორმაზე დაიყვანება (თ/ტ → t,
///     ქ/კ/ყ/ხ → k, ფ/პ → p, ც/წ → ts, ჩ/ჭ → ch, ღ → g), რომ "BERIDZE GIORGI", "Gogitchaishvili" და "გიორგი ბერიძე"
///     ერთმანეთს დაემთხვეს. სიტყვა მხოლოდ ასოებია, დანარჩენი გამყოფია
/// </summary>
public static class LatinSkeleton
{
    private static readonly Dictionary<char, string> GeorgianLetters = new()
    {
        ['ა'] = "a",
        ['ბ'] = "b",
        ['გ'] = "g",
        ['დ'] = "d",
        ['ე'] = "e",
        ['ვ'] = "v",
        ['ზ'] = "z",
        ['თ'] = "t",
        ['ი'] = "i",
        ['კ'] = "k",
        ['ლ'] = "l",
        ['მ'] = "m",
        ['ნ'] = "n",
        ['ო'] = "o",
        ['პ'] = "p",
        ['ჟ'] = "zh",
        ['რ'] = "r",
        ['ს'] = "s",
        ['ტ'] = "t",
        ['უ'] = "u",
        ['ფ'] = "p",
        ['ქ'] = "k",
        ['ღ'] = "g",
        ['ყ'] = "k",
        ['შ'] = "sh",
        ['ჩ'] = "ch",
        ['ც'] = "ts",
        ['ძ'] = "dz",
        ['წ'] = "ts",
        ['ჭ'] = "ch",
        ['ხ'] = "k",
        ['ჯ'] = "j",
        ['ჰ'] = "h"
    };

    //ლათინური ჩაწერის ვარიანტები, თანმიმდევრობით (tch → ch უნდა იყოს c → ts-მდე)
    private static readonly (string From, string To)[] LatinVariants =
    [
        ("tch", "ch"), ("dj", "j"), ("kh", "k"), ("gh", "g"), ("ph", "p"), ("th", "t"), ("tz", "ts"), ("x", "k"),
        ("q", "k"), ("w", "v"), ("y", "i")
    ];

    /// <summary>
    ///     ტექსტის სიტყვები ჩონჩხის ფორმით, ტექსტის რიგით
    /// </summary>
    public static List<string> Tokens(string? text)
    {
        List<string> tokens = [];
        if (string.IsNullOrEmpty(text))
        {
            return tokens;
        }

        var word = new StringBuilder();
        foreach (char original in text)
        {
            char letter = char.ToLowerInvariant(original);
            if (GeorgianLetters.TryGetValue(letter, out string? latin))
            {
                word.Append(latin);
            }
            else if (letter is >= 'a' and <= 'z')
            {
                word.Append(letter);
            }
            else
            {
                AddWord(tokens, word);
            }
        }

        AddWord(tokens, word);
        return tokens;
    }

    private static void AddWord(List<string> tokens, StringBuilder word)
    {
        if (word.Length == 0)
        {
            return;
        }

        tokens.Add(NormalizeLatin(word.ToString()));
        word.Clear();
    }

    private static string NormalizeLatin(string word)
    {
        foreach ((string from, string to) in LatinVariants)
        {
            word = word.Replace(from, to, false, CultureInfo.InvariantCulture);
        }

        //c, რომელსაც h არ მოსდევს, ც-ა (Ciklauri = Tsiklauri); ch უცვლელია
        var result = new StringBuilder(word.Length + 2);
        for (int i = 0; i < word.Length; i++)
        {
            if (word[i] == 'c' && (i + 1 >= word.Length || word[i + 1] != 'h'))
            {
                result.Append("ts");
            }
            else
            {
                result.Append(word[i]);
            }
        }

        return result.ToString();
    }
}
