using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AppMimosiGe.Application.StudentContracts.Models;

/// <summary>
///     კონტრაქტის ნომერი "პ.ააა": პრეფიქსი სასწავლო წლის დაწყების ბოლო ციფრია (Access-ში ასე ინომრებოდა: 2026 → 6.001),
///     ააა კი წლის რიგითი ნომერი
/// </summary>
public static class ContractNumbers
{
    private const int MaxSequence = 999;

    public static string Prefix(DateTime academicYearStartDate)
    {
        return (academicYearStartDate.Year % 10).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///     შემდეგი თავისუფალი ნომერი: ამ პრეფიქსიანი ნომრების მაქსიმუმი + 1 (სხვა ფორმატის ან სხვა პრეფიქსის ნომრები არ
    ///     ითვლება). null: 999 დაკავებულია
    /// </summary>
    public static string? Next(DateTime academicYearStartDate, IEnumerable<string> existingNumbers)
    {
        string prefix = Prefix(academicYearStartDate);
        int max = existingNumbers.Select(n => Sequence(prefix, n)).DefaultIfEmpty(0).Max();
        return max >= MaxSequence
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{prefix}.{max + 1:000}");
    }

    private static int Sequence(string prefix, string number)
    {
        return number.Length == 5 && number.StartsWith(prefix + ".", StringComparison.Ordinal) &&
               int.TryParse(number.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out int sequence)
            ? sequence
            : 0;
    }
}
