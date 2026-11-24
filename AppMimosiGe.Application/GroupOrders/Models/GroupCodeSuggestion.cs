using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის კოდის შეთავაზება შექმნის ბრძანებისთვის (D186): „{კლასი}{NN}". კლასი მოსწავლის სტატუსის Rate-იდან მოდის
///     (101–112 → 1–12, 212 → 12, 200 → 13, 312 → 14), NN კი სასწავლო წელში ამ კლასის პირველი თავისუფალი ორნიშნა
///     ნომერია (01–99). კოდი წელში უნიკალურია (D11), დაპაუზებული შექმნის ბრძანებების კოდების ჩათვლით
/// </summary>
public static class GroupCodeSuggestion
{
    private const int MaxNumber = 99;

    //სტატუსები 101–112 კლასებია 1–12
    private const int GradeRateBase = 100;

    /// <summary>
    ///     კლასი სტატუსის Rate-იდან; null, თუ სტატუსს კლასი არ შეესაბამება
    /// </summary>
    public static int? SchoolClass(int studentStatusRate)
    {
        return studentStatusRate switch
        {
            >= 101 and <= 112 => studentStatusRate - GradeRateBase,
            212 => 12,
            200 => 13,
            312 => 14,
            _ => null
        };
    }

    /// <summary>
    ///     takenCodes: სასწავლო წლის ჯგუფებისა და დაპაუზებული შექმნის ბრძანებების კოდები, ნებისმიერი კლასისა. null, თუ
    ///     სტატუსს კლასი არ შეესაბამება ან კლასის ყველა ნომერი დაკავებულია
    /// </summary>
    public static string? Suggest(int studentStatusRate, IEnumerable<string> takenCodes)
    {
        if (SchoolClass(studentStatusRate) is not { } schoolClass)
        {
            return null;
        }

        HashSet<string> taken = [with(StringComparer.Ordinal), .. takenCodes.Select(c => c.Trim())];
        return Enumerable.Range(1, MaxNumber)
            .Select(number => string.Create(CultureInfo.InvariantCulture, $"{schoolClass}{number:00}"))
            .FirstOrDefault(code => !taken.Contains(code));
    }
}
