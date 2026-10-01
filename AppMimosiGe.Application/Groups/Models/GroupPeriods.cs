using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Requests;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის მასწავლებლების, მოსწავლეებისა და განრიგის პერიოდები [StartDate, EndDate): დაწყების დღე შედის,
///     დასრულების დღე აღარ შედის, EndDate null დაუსრულებელ პერიოდს ნიშნავს (Access-ის GroupTeacherData.IsValid)
/// </summary>
public static class GroupPeriods
{
    public static bool Overlap(DateTime start1, DateTime? end1, DateTime start2, DateTime? end2)
    {
        return start1.Date < (end2?.Date ?? DateTime.MaxValue) && start2.Date < (end1?.Date ?? DateTime.MaxValue);
    }

    //გაკვეთილების გენერატორის შეცდომა 5: ერთ დღეს ორი მასწავლებელი
    public static bool AnyTeacherPeriodsOverlap(IReadOnlyList<GroupTeacherRequest> teachers)
    {
        return AnyPairMatches(teachers, (a, b) => Overlap(a.StartDate, a.EndDate, b.StartDate, b.EndDate));
    }

    //გაკვეთილების გენერატორის შეცდომა 7: ერთ კვირის დღეზე ორი განრიგი
    public static bool AnyDayTimePlacePeriodsOverlap(IReadOnlyList<GroupDayTimePlaceRequest> dayTimePlaces)
    {
        return AnyPairMatches(dayTimePlaces,
            (a, b) => a.WeekDayId == b.WeekDayId && Overlap(a.StartDate, a.EndDate, b.StartDate, b.EndDate));
    }

    //ერთი კონტრაქტი ერთ დღეს ჯგუფში ორჯერ: Access-ის გენერატორის გაკვეთილის მოსწავლეების შედარება ამას ვერ არჩევდა (D64)
    public static bool AnyStudentPeriodsOverlap(IReadOnlyList<GroupStudentRequest> students)
    {
        return AnyPairMatches(students,
            (a, b) => a.StudentContractId == b.StudentContractId &&
                      Overlap(a.StartDate, a.EndDate, b.StartDate, b.EndDate));
    }

    private static bool AnyPairMatches<T>(IReadOnlyList<T> rows, Func<T, T, bool> match)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            for (int j = i + 1; j < rows.Count; j++)
            {
                if (match(rows[i], rows[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
