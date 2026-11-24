using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     განრიგის ბრძანებების ეფექტები (D188): განრიგის შეცვლა (6) და კვირის საათების შეცვლა (10). ორივე a-ზე მოქმედი
///     განრიგის ყველა სტრიქონს a-თი ასრულებს და a-დან ბრძანების განრიგს იწყებს. ახალი განრიგი მოქმედს არ უნდა
///     ემთხვეოდეს და a-ს კვირის დღე მის ერთ-ერთ დღეს უნდა ემთხვეოდეს
/// </summary>
public static class GroupScheduleOrderEffects
{
    /// <summary>
    ///     6: განრიგი იცვლება, კვირის საათების ჯამი კი იგივე რჩება: საათები მხოლოდ ბრძანება 10-ით იცვლება
    /// </summary>
    public static Result<GroupState> ChangeSchedule(GroupState state, GroupOrderData order)
    {
        return ReplaceSchedule(state, order, keepsWeeklyHours: true);
    }

    /// <summary>
    ///     10: განრიგი იცვლება ჯამის შეზღუდვის გარეშე. ბრძანებაში ყველა მოსწავლეა, ვინც a-ზე ჯგუფშია, და მათ სტრიქონებს
    ///     ბრძანება 8-ის წესი ცვლის, ახალი განრიგით გადათვლილი საათებით (D209): ტარიფი თუ არ იცვლება, შენიშვნა ადგილზე
    ///     იწერება
    /// </summary>
    public static Result<GroupState> ChangeWeeklyHours(GroupState state, GroupOrderData order)
    {
        Result<GroupState> scheduled = ReplaceSchedule(state, order, keepsWeeklyHours: false);
        if (scheduled.IsFailure)
        {
            return scheduled;
        }

        //ბრძანებაში ზედმეტ მოსწავლეს 8-ის წესი უარყოფს (StudentIsNotInGroup), აკლებულს კი ეს შემოწმება
        if (state.Students.Any(s => GroupActivity.IsRowActiveOn(s.StartDate, s.EndDate, order.OrderDate) &&
                                    order.Students.All(l => l.StudentContractId != s.StudentContractId)))
        {
            return GroupOrderErrors.StudentsMustBeAllCurrent;
        }

        return GroupStudentOrderEffects.ChangeStudentFees(scheduled.Value, order);
    }

    //a-ზე მოქმედი განრიგი a-თი სრულდება, ბრძანების განრიგი კი a-დან იწყება. keepsWeeklyHours: ჯამი არ იცვლება (6)
    private static Result<GroupState> ReplaceSchedule(GroupState state, GroupOrderData order, bool keepsWeeklyHours)
    {
        DateTime orderDate = order.OrderDate;
        List<GroupDayTimePlaceRow> current = [.. state.DayTimePlaces.Where(d => IsActiveOn(d, orderDate))];
        if (IsSameSchedule(current, order.DayTimePlaces))
        {
            return GroupOrderErrors.ScheduleIsAlreadyCurrent;
        }

        if (keepsWeeklyHours && GeneratorHours.AreDifferent(current.Sum(d => d.HoursCount),
                order.DayTimePlaces.Sum(d => d.HoursCount)))
        {
            return GroupOrderErrors.WeeklyHoursMustNotChange;
        }

        if (order.DayTimePlaces.All(d => d.WeekDayId != WeekDayId(orderDate)))
        {
            return GroupOrderErrors.OrderDateWeekDayIsNotInSchedule;
        }

        //D207: a-ზე დაწყებული სტრიქონი a-თი ვერ დასრულდება, როგორც მასწავლებლისა და მოსწავლისა (D206)
        if (current.Exists(d => d.StartDate.Date == orderDate.Date))
        {
            return GroupOrderErrors.RowStartsOnOrderDate;
        }

        //D208: ახალი განრიგი ძველის დასრულებას იღებს: თუ ძველის სტრიქონები სხვადასხვა დღეს სრულდება, უგვიანესს, ხოლო
        //თუ ერთი მაინც ღიაა, ახალიც ღიაა. დაგეგმილ სტრიქონებთან კონფლიქტს წესები ბლოკავს (D55, საათების ფორმულა)
        DateTime? endDate = current.Exists(d => d.EndDate is null) ? null : current.Max(d => d.EndDate);
        return state with
        {
            DayTimePlaces =
            [
                .. state.DayTimePlaces.Select(d => IsActiveOn(d, orderDate) ? d with { EndDate = orderDate } : d),
                .. order.DayTimePlaces.Select(d => d.ToRow(orderDate, endDate))
            ]
        };
    }

    //ახალი განრიგი მოქმედს ემთხვევა: იგივე დღეები, დროები, ოთახები და საათები, რიგის მიუხედავად (საათები ზღვრით, D153)
    private static bool IsSameSchedule(List<GroupDayTimePlaceRow> current,
        IReadOnlyList<GroupOrderDayTimePlaceLine> lines)
    {
        if (current.Count != lines.Count)
        {
            return false;
        }

        List<GroupDayTimePlaceRow> unmatched = [.. current];
        foreach (GroupOrderDayTimePlaceLine line in lines)
        {
            int index = unmatched.FindIndex(d =>
                d.WeekDayId == line.WeekDayId && d.LessonStartTimeId == line.LessonStartTimeId &&
                d.RoomId == line.RoomId && !GeneratorHours.AreDifferent(d.HoursCount, line.HoursCount));
            if (index < 0)
            {
                return false;
            }

            unmatched.RemoveAt(index);
        }

        return true;
    }

    private static bool IsActiveOn(GroupDayTimePlaceRow row, DateTime day)
    {
        return GroupActivity.IsRowActiveOn(row.StartDate, row.EndDate, day);
    }

    //კვირის დღის ID (WeekDays): 1 = ორშაბათი … 7 = კვირა, როგორც გენერატორის WeekDayNumber-ში
    private static int WeekDayId(DateTime day)
    {
        return ((int)day.DayOfWeek + 6) % 7 + 1;
    }
}
