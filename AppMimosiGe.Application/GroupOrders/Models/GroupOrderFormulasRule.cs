using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების რეჟიმის ახალი წესები (D187, D194): მოსწავლის სტრიქონის 4 კვირის საათები ფორმულას ესადაგება, 4 კვირის
///     გადასახადი კი საათის ფასისა და საათების ნამრავლს (GroupOrderFormulas). მოწმდება გადასვლის დღიდან (D0) ყოველ
///     დღეს, როცა მოსწავლის სტრიქონიც და განრიგიც მოქმედებს; D0-მდე ისტორია არ მოწმდება. შედეგი მხოლოდ სტრიქონის
///     დაწყების და განრიგის ცვლილების დღეებში იცვლება, ამიტომ მოწმდება ეს დღეები, როგორც r30-ში
///     (MismatchReports.StudentFeeMismatches)
/// </summary>
public static class GroupOrderFormulasRule
{
    /// <summary>
    ///     state-ის დარღვევები: სტრიქონსა და ფორმულაზე ერთი, პირველი დღით. რიგი: დღე, სტრიქონის ხაზი, ფორმულა (ჯერ
    ///     საათები). transitionDate: გადასვლის დღე (D0, GroupOrdersMode.TransitionDate)
    /// </summary>
    public static IReadOnlyList<GroupOrderFormulaViolation> Find(GroupState state, DateTime transitionDate)
    {
        List<GroupOrderFormulaViolation> violations = [];
        foreach (GroupStudentRow row in state.Students)
        {
            List<(DateTime Day, float WeeklyHours)> days = CheckDays(row, state.DayTimePlaces, transitionDate.Date);
            if (days.Count == 0)
            {
                continue;
            }

            int hoursIndex = days.FindIndex(d =>
                !GroupOrderFormulas.HoursMatchSchedule(row.FourWeekHours, row.HoursCoefficient, d.WeeklyHours));
            if (hoursIndex >= 0)
            {
                violations.Add(new GroupOrderFormulaViolation(EGroupOrderFormula.Hours, state.Fields.GroupCode, row,
                    days[hoursIndex].Day, days[hoursIndex].WeeklyHours));
            }

            //ფასი სტრიქონის საათებით მოწმდება, ამიტომ ყველა დღეს ერთნაირია
            if (!GroupOrderFormulas.FeeMatchesHours(row.FourWeekFee, row.OneHourFee, row.FourWeekHours))
            {
                violations.Add(new GroupOrderFormulaViolation(EGroupOrderFormula.Fee, state.Fields.GroupCode, row,
                    days[0].Day, days[0].WeeklyHours));
            }
        }

        return [.. violations.OrderBy(v => v.Date).ThenBy(v => v.Row.LineId).ThenBy(v => v.Formula)];
    }

    /// <summary>
    ///     შენახვის შეცდომა (409): საათების დარღვევები, ხოლო თუ ისინი არ არის, ფასისა; null, თუ დარღვევა არ არის. საათები
    ///     პირველია, რადგან ფასი სტრიქონის საათებით მოწმდება. describe: დარღვევის აღწერა მოსწავლის სახელით (Describe)
    /// </summary>
    public static Error? ToError(IEnumerable<GroupOrderFormulaViolation> violations,
        Func<GroupOrderFormulaViolation, string> describe)
    {
        List<GroupOrderFormulaViolation> all = [.. violations];
        List<string> hours = [.. all.Where(v => v.Formula == EGroupOrderFormula.Hours).Select(describe)];
        if (hours.Count > 0)
        {
            return GroupOrderErrors.StudentHoursDoNotMatchSchedule(hours);
        }

        List<string> fees = [.. all.Where(v => v.Formula == EGroupOrderFormula.Fee).Select(describe)];
        return fees.Count > 0 ? GroupOrderErrors.StudentFeeDoesNotMatchHours(fees) : null;
    }

    //დღეები D0-დან, როცა სტრიქონი და განრიგი მოქმედებს, კვირის საათებით: სტრიქონის დაწყება (ან D0) და განრიგის
    //სტრიქონების დაწყება და დასრულება. მათ შორის კვირის საათები არ იცვლება
    private static List<(DateTime Day, float WeeklyHours)> CheckDays(GroupStudentRow row,
        IReadOnlyList<GroupDayTimePlaceRow> dayTimePlaces, DateTime firstDay)
    {
        IEnumerable<DateTime?> changeDays =
        [
            row.StartDate.Date > firstDay ? row.StartDate.Date : firstDay,
            .. dayTimePlaces.SelectMany(d => new DateTime?[] { d.StartDate, d.EndDate })
        ];
        return
        [
            .. changeDays.OfType<DateTime>().Select(d => d.Date).Distinct().Order()
                .Where(day => day >= firstDay && GroupActivity.IsRowActiveOn(row.StartDate, row.EndDate, day) &&
                              dayTimePlaces.Any(d => GroupActivity.IsRowActiveOn(d.StartDate, d.EndDate, day)))
                .Select(day => (day, GroupOrderFormulas.WeeklyHours(dayTimePlaces, day)))
        ];
    }
}
