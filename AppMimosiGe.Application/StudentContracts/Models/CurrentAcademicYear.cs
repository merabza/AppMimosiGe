using System;
using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.StudentContracts.Models;

public static class CurrentAcademicYear
{
    /// <summary>
    ///     მიმდინარე სასწავლო წელი: რომლის [StartDate, FinishDate) შუალედშიც არის დღევანდელი თარიღი.
    ///     თუ ასეთი არ არის (მაგალითად, წლებს შორის შუალედი), ბოლო დაწყებული წელი, თუ არც ასეთია, ყველაზე ადრეული. Access-ში ნაგულისხმევი წელი ფორმაში hard-coded იყო (9)
    /// </summary>
    public static int? Find(IReadOnlyCollection<AcademicYear> academicYears, DateTime today)
    {
        AcademicYear? current =
            academicYears.Where(ay => ay.StartDate <= today && today < ay.FinishDate).MaxBy(ay => ay.StartDate) ??
            academicYears.Where(ay => ay.StartDate <= today).MaxBy(ay => ay.StartDate) ??
            academicYears.MinBy(ay => ay.StartDate);
        return current?.AyId;
    }
}
