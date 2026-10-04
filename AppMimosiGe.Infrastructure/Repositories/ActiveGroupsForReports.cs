using System;
using System.Linq;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

/// <summary>
///     Access-ის vActiveGroupsForReports (რეპორტების საერთო საფუძველი, D116): ჯგუფები, რომლებიც date დღეს
///     გაუქმებული არ არის და რომლებშიც ამ დღეს მოქმედებს მოსწავლის, მასწავლებლის და განრიგის თითო სტრიქონი მაინც.
///     "მოქმედებს" = [StartDate, EndDate) შეიცავს დღეს: Access-ის StartDate &lt;= P და (EndDate Is Null Or EndDate &gt;
///     P),
///     სადაც P დღის ბოლოა (txtEndDate-ის ნაგულისხმევი 23:59:59). ბაზაში თარიღები დროის გარეშეა, ამიტომ ეს
///     დღის ნებისმიერ დროზე ერთნაირია. ნაწილები 17 და 18 ამ query-ს იყენებენ (Infrastructure-ის რეპოზიტორიებიდან)
/// </summary>
public static class ActiveGroupsForReports
{
    public static IQueryable<Group> Query(IMimosiGeDbContext context, DateTime date)
    {
        DateTime dayAfter = DayAfter(date);
        return context.Groups.Where(g => (g.VoidDate == null || g.VoidDate >= dayAfter) &&
                                         g.GroupsByStudents.Any(s =>
                                             s.StartDate < dayAfter && (s.EndDate == null || s.EndDate >= dayAfter)) &&
                                         g.GroupsByTeachers.Any(t =>
                                             t.StartDate < dayAfter && (t.EndDate == null || t.EndDate >= dayAfter)) &&
                                         g.GroupDayTimePlaces.Any(d =>
                                             d.StartDate < dayAfter && (d.EndDate == null || d.EndDate >= dayAfter)));
    }

    //დღის შემდეგი დღის დასაწყისი: სტრიქონი date დღეს მოქმედებს, თუ StartDate < DayAfter და EndDate >= DayAfter
    public static DateTime DayAfter(DateTime date)
    {
        return date.Date.AddDays(1);
    }
}
