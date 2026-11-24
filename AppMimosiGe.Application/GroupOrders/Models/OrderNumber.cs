using System;
using System.Globalization;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების ნომერი „{yy}.{n}" (D192): yy ჯგუფის სასწავლო წლის დაწყების წლის ბოლო ორი ციფრია (2026–2027 → 26), n
///     რიგითია ამ წლის შიგნით, ნულების გარეშე (26.1, 26.15). ტექსტად იწერება, მაგრამ რიცხვებით ლაგდება: ჯერ წელი, მერე
///     n (26.9 &lt; 26.10)
/// </summary>
public readonly record struct OrderNumber(int NumberYear, int Number) : IComparable<OrderNumber>
{
    private const int YearDigitsDivisor = 100;

    public static bool operator <(OrderNumber left, OrderNumber right) => left.CompareTo(right) < 0;

    public static bool operator >(OrderNumber left, OrderNumber right) => left.CompareTo(right) > 0;

    public static bool operator <=(OrderNumber left, OrderNumber right) => left.CompareTo(right) <= 0;

    public static bool operator >=(OrderNumber left, OrderNumber right) => left.CompareTo(right) >= 0;

    public int CompareTo(OrderNumber other)
    {
        int byYear = NumberYear.CompareTo(other.NumberYear);
        return byYear != 0 ? byYear : Number.CompareTo(other.Number);
    }

    /// <summary>
    ///     ნომრის წელი სასწავლო წლის დაწყების თარიღიდან (AcademicYear.StartDate): წლის ბოლო ორი ციფრი
    /// </summary>
    public static int YearOf(DateTime academicYearStartDate)
    {
        return academicYearStartDate.Year % YearDigitsDivisor;
    }

    /// <summary>
    ///     წლის შემდეგი ნომერი მთვლელიდან (GroupOrderNumbers.LastNumber; null: ამ წელს ნომერი ჯერ არ გაცემულა)
    /// </summary>
    public static OrderNumber Next(int numberYear, int? lastNumber)
    {
        return new OrderNumber(numberYear, (lastNumber ?? 0) + 1);
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{NumberYear:00}.{Number}");
    }
}
