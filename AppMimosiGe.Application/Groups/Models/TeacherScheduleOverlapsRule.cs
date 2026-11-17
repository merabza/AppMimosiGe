using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     მასწავლებლების დროების გადაფარვის წესი (რეპორტი r18 "მასწავლებლების დროების გადაფარვა", D178): მასწავლებელს ორ
///     ჯგუფში კვირის ერთსა და იმავე დღეს დროით გადამფარავი გაკვეთილი [დაწყება, დაწყება + საათები) აქვს იმ დღეებში, როცა
///     ორივე ჯგუფში მოქმედებს მისი სტრიქონიც და ამ კვირის დღის განრიგის სტრიქონიც, ჯგუფი კი გაუქმებული არ არის
///     (თარიღისთვის მოქმედი განრიგი, როგორც r18-ში). r18-ისგან განსხვავებით მოსწავლეები არ მოწმდება: მოსწავლის გარეშე
///     ჯგუფიც იკავებს მასწავლებლის დროს. ჯგუფის რედაქტორი არ ინახავს ჯგუფს, რომლის მასწავლებელსაც სხვა ჯგუფთან
///     გადაფარვა აქვს, მათ შორის უკვე არსებულიც. ერთი ჯგუფის შიგნით გადაფარვას ვალიდატორი არ უშვებს (D55)
/// </summary>
public static class TeacherScheduleOverlapsRule
{
    /// <summary>
    ///     scheduleGroup-ის (შენახვის შემდგომი მდგომარეობით) გადაფარვები სხვა ჯგუფებთან. იმავე GrpId-ის ჯგუფი არ ედრება.
    ///     რიგი: პირველი დღე, სხვა ჯგუფის კოდი, კვირის დღე, სხვა ჯგუფის დრო (ერთ ჯგუფში ერთ დღეს ორი მასწავლებელი
    ///     ვალიდატორით ვერ იქნება, ამიტომ მასწავლებლით აღარ ლაგდება)
    /// </summary>
    public static IReadOnlyList<TeacherScheduleOverlap> Find(TeacherScheduleGroup scheduleGroup,
        IEnumerable<TeacherScheduleGroup> otherGroups)
    {
        List<Occupancy> own = [.. Occupancies(scheduleGroup)];
        List<TeacherScheduleOverlap> overlaps = [];
        foreach (TeacherScheduleGroup other in otherGroups.Where(g => g.GrpId != scheduleGroup.GrpId))
        {
            foreach (Occupancy theirs in Occupancies(other))
            {
                overlaps.AddRange(own.Where(mine => Overlap(mine, theirs)).Select(mine =>
                {
                    (DateTime fromDate, DateTime? toDate) = CommonDays(mine, theirs);
                    return new TeacherScheduleOverlap(mine.TeacherContractId, other.GrpId, other.GroupCode,
                        theirs.DayTime.WeekDayId, theirs.DayTime.StartTime, theirs.DayTime.EndTime, fromDate, toDate);
                }));
            }
        }

        return
        [
            .. overlaps.OrderBy(o => o.FromDate).ThenBy(o => o.OtherGroupCode, StringComparer.Ordinal)
                .ThenBy(o => o.WeekDayId).ThenBy(o => o.OtherStartTime)
        ];
    }

    //მასწავლებლის სტრიქონი × განრიგის სტრიქონი, თუ მათ საერთო დღე აქვთ
    private static IEnumerable<Occupancy> Occupancies(TeacherScheduleGroup scheduleGroup)
    {
        return scheduleGroup.Teachers
            .SelectMany(_ => scheduleGroup.DayTimes,
                (teacher, dayTime) => CreateOccupancy(teacher, dayTime, scheduleGroup.VoidDate))
            .Where(o => o.EndDate is null || o.StartDate < o.EndDate);
    }

    //დღეები [StartDate, EndDate), როცა მასწავლებლის და განრიგის სტრიქონი ორივე მოქმედებს და ჯგუფი გაუქმებული არ არის.
    //EndDate null დაუსრულებელს ნიშნავს: nullable-ის Min null-ებს არ ითვლის
    private static Occupancy CreateOccupancy(TeacherScheduleTeacher teacher, TeacherScheduleDayTime dayTime,
        DateTime? voidDate)
    {
        DateTime[] starts = [teacher.StartDate, dayTime.StartDate];
        DateTime?[] ends = [teacher.EndDate, dayTime.EndDate, voidDate];
        return new Occupancy(teacher.TeacherContractId, dayTime, starts.Max(), ends.Min());
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
        return mine.TeacherContractId == theirs.TeacherContractId &&
               mine.DayTime.WeekDayId == theirs.DayTime.WeekDayId && mine.DayTime.Start < theirs.DayTime.End &&
               theirs.DayTime.Start < mine.DayTime.End &&
               GroupPeriods.Overlap(mine.StartDate, mine.EndDate, theirs.StartDate, theirs.EndDate);
    }

    //მასწავლებლის გაკვეთილი განრიგში: კვირის დღე და დრო, დღეები [StartDate, EndDate)
    private sealed record Occupancy(
        int TeacherContractId,
        TeacherScheduleDayTime DayTime,
        DateTime StartDate,
        DateTime? EndDate);
}
