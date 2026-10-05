using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.StudentContracts.Models;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.AcademicYears.Models;

/// <summary>
///     ახალი სასწავლო წელი: მიმდინარე წლის (CurrentAcademicYear, D47) მომდევნო. დაწყება = მიმდინარე წლის დასრულება,
///     დასრულება = დაწყება + 1 წელი, სახელი "YYYY-YYYY" (Access-ის წლების ფორმატი, მაგ. 2026-2027). მიმდინარე წლიდან
///     ითვლება და არა ბოლოდან, ამიტომ განმეორებით გაშვება იმავე წელს იძლევა და მეორე წელს აღარ ამატებს
/// </summary>
public sealed record NextAcademicYear(string AcademicYearName, DateTime StartDate, DateTime FinishDate)
{
    //null: წლები არ არის, ამიტომ მომდევნო წელი ვერ დადგინდება
    public static NextAcademicYear? Plan(IReadOnlyCollection<AcademicYear> academicYears, DateTime today)
    {
        int? currentId = CurrentAcademicYear.Find(academicYears, today);
        AcademicYear? current = academicYears.SingleOrDefault(ay => ay.AyId == currentId);
        if (current is null)
        {
            return null;
        }

        DateTime startDate = current.FinishDate.Date;
        return new NextAcademicYear($"{startDate.Year}-{startDate.Year + 1}", startDate, startDate.AddYears(1));
    }

    public bool ExistsIn(IEnumerable<AcademicYear> academicYears)
    {
        return academicYears.Any(ay => string.Equals(ay.AcademicYearName.Trim(), AcademicYearName,
            StringComparison.OrdinalIgnoreCase));
    }
}
