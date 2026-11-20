using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფების განრიგის დროების გადაფარვის წესი: მასწავლებლებისა (რეპორტი r18 "მასწავლებლების დროების გადაფარვა",
///     D178) და მოსწავლეების (რეპორტი r19 "მოსწავლეების დროების გადაფარვა", D179). მასწავლებელს ან მოსწავლეს ორ ჯგუფში
///     კვირის ერთსა და იმავე დღეს დროით გადამფარავი გაკვეთილი [დაწყება, დაწყება + საათები) აქვს იმ დღეებში, როცა ორივე
///     ჯგუფში მოქმედებს მისი სტრიქონიც და ამ კვირის დღის განრიგის სტრიქონიც, ჯგუფი კი გაუქმებული არ არის (თარიღისთვის
///     მოქმედი განრიგი, როგორც r18-სა და r19-ში). რეპორტებისგან განსხვავებით მეორე მხარე არ მოწმდება: მოსწავლის გარეშე
///     ჯგუფიც იკავებს მასწავლებლის დროს, მასწავლებლის გარეშე ჯგუფი კი მოსწავლისას. ჯგუფის რედაქტორი არ ინახავს ჯგუფს,
///     რომლის მასწავლებელსაც ან მოსწავლესაც სხვა ჯგუფთან გადაფარვა აქვს დღეს ან მომავალში, მათ შორის უკვე არსებულიც.
///     დღემდე დასრულებული გადაფარვა შენახვას არ აბრკოლებს (D203: დროებით, ბრძანებებზე გადასვლისას გამკაცრდება). ერთი
///     ჯგუფის შიგნით გადაფარვას ვალიდატორი არ უშვებს (D55)
/// </summary>
public static class GroupScheduleOverlapsRule
{
    /// <summary>
    ///     schedule-ის (შენახვის შემდგომი მდგომარეობით) გადაფარვები სხვა ჯგუფებთან, რომელთა დღეები today-ს ან მის
    ///     შემდეგ დღეებსაც ეხება (D203); ასეთი გადაფარვა თავისი ყველა დღით ბრუნდება. იმავე GrpId-ის ჯგუფი არ ედრება.
    ///     რიგი: პირველი დღე, სხვა ჯგუფის კოდი, კვირის დღე, სხვა ჯგუფის დრო, კონტრაქტი (ერთ ჯგუფში ერთ დღეს ორი
    ///     მასწავლებელი ვალიდატორით ვერ იქნება, მოსწავლე კი ბევრია)
    /// </summary>
    public static IReadOnlyList<GroupScheduleOverlap> Find(GroupSchedule schedule,
        IEnumerable<GroupSchedule> otherSchedules, DateTime today)
    {
        List<Occupancy> own = [.. Occupancies(schedule)];
        List<GroupScheduleOverlap> overlaps = [];
        foreach (GroupSchedule other in otherSchedules.Where(g => g.GrpId != schedule.GrpId))
        {
            foreach (Occupancy theirs in Occupancies(other))
            {
                overlaps.AddRange(own.Where(mine => Overlap(mine, theirs)).Select(mine =>
                {
                    (DateTime fromDate, DateTime? toDate) = CommonDays(mine, theirs);
                    return new GroupScheduleOverlap(mine.ContractId, other.GrpId, other.GroupCode,
                        theirs.DayTime.WeekDayId, theirs.DayTime.StartTime, theirs.DayTime.EndTime, fromDate, toDate);
                }));
            }
        }

        return
        [
            .. overlaps.Where(o => IsCurrentOrFuture(o, today)).OrderBy(o => o.FromDate)
                .ThenBy(o => o.OtherGroupCode, StringComparer.Ordinal).ThenBy(o => o.WeekDayId)
                .ThenBy(o => o.OtherStartTime).ThenBy(o => o.ContractId)
        ];
    }

    //გადაფარვის დღეებიდან [FromDate, ToDate) ერთი მაინც today-ა ან მის შემდეგაა
    private static bool IsCurrentOrFuture(GroupScheduleOverlap overlap, DateTime today)
    {
        return GroupPeriods.Overlap(overlap.FromDate, overlap.ToDate, today, null);
    }

    //წევრის (მასწავლებლის ან მოსწავლის) სტრიქონი × განრიგის სტრიქონი, თუ მათ საერთო დღე აქვთ
    private static IEnumerable<Occupancy> Occupancies(GroupSchedule schedule)
    {
        return schedule.Members
            .SelectMany(_ => schedule.DayTimes,
                (member, dayTime) => CreateOccupancy(member, dayTime, schedule.VoidDate))
            .Where(o => o.EndDate is null || o.StartDate < o.EndDate);
    }

    //დღეები [StartDate, EndDate), როცა წევრის და განრიგის სტრიქონი ორივე მოქმედებს და ჯგუფი გაუქმებული არ არის.
    //EndDate null დაუსრულებელს ნიშნავს: nullable-ის Min null-ებს არ ითვლის
    private static Occupancy CreateOccupancy(GroupScheduleMember member, GroupScheduleDayTime dayTime,
        DateTime? voidDate)
    {
        DateTime[] starts = [member.StartDate, dayTime.StartDate];
        DateTime?[] ends = [member.EndDate, dayTime.EndDate, voidDate];
        return new Occupancy(member.ContractId, dayTime, starts.Max(), ends.Min());
    }

    //დღეები, როცა ორივე გაკვეთილი განრიგშია: გვიანი დაწყებიდან ადრეულ დასრულებამდე
    private static (DateTime FromDate, DateTime? ToDate) CommonDays(Occupancy mine, Occupancy theirs)
    {
        DateTime[] starts = [mine.StartDate, theirs.StartDate];
        DateTime?[] ends = [mine.EndDate, theirs.EndDate];
        return (starts.Max(), ends.Min());
    }

    private static bool Overlap(Occupancy mine, Occupancy theirs)
    {
        return mine.ContractId == theirs.ContractId && mine.DayTime.WeekDayId == theirs.DayTime.WeekDayId &&
               mine.DayTime.Start < theirs.DayTime.End && theirs.DayTime.Start < mine.DayTime.End &&
               GroupPeriods.Overlap(mine.StartDate, mine.EndDate, theirs.StartDate, theirs.EndDate);
    }

    //წევრის გაკვეთილი განრიგში: კვირის დღე და დრო, დღეები [StartDate, EndDate)
    private sealed record Occupancy(
        int ContractId,
        GroupScheduleDayTime DayTime,
        DateTime StartDate,
        DateTime? EndDate);
}
