using System;
using System.Collections.Generic;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     მოსწავლის ბრძანებების ეფექტები: დამატება (3), გაყვანა (4), გადაყვანა (5) და საფასურის შეცვლა (8). ბრძანება
///     რამდენიმე მოსწავლეს ეხება; მოსწავლეს ჯგუფში ერთ დღეს ერთი სტრიქონი აქვს (D64). შენიშვნა ხაზიდან იწერება (D189).
///     ახალი სტრიქონის 4 კვირის საათები ფორმულით ითვლება მისი ჯგუფის a-ზე მოქმედი განრიგიდან (D187)
/// </summary>
public static class GroupStudentOrderEffects
{
    /// <summary>
    ///     3 (D190): ახალი სტრიქონები a-დან, დასრულების გარეშე. მოსწავლე a-ზე ამ ჯგუფში უკვე არ უნდა იყოს
    /// </summary>
    public static Result<GroupState> AddStudents(GroupState state, GroupOrderData order)
    {
        float weeklyHours = GroupOrderFormulas.WeeklyHours(state.DayTimePlaces, order.OrderDate);
        List<GroupStudentRow> students = [.. state.Students];
        foreach (GroupOrderStudentLine line in order.Students)
        {
            if (ActiveRowIndex(students, line.StudentContractId, order.OrderDate) >= 0)
            {
                return GroupOrderErrors.StudentIsAlreadyInGroup;
            }

            students.Add(line.ToRow(order.OrderDate, null, weeklyHours));
        }

        return state with { Students = students };
    }

    /// <summary>
    ///     4 (D189, D190): a-ზე მოქმედი სტრიქონი a-თი სრულდება და შენიშვნა მასში იწერება. შეიძლება ყველა მოსწავლის
    ///     გაყვანაც: ჯგუფი ღია რჩება, სანამ ბრძანება 7 არ დახურავს
    /// </summary>
    public static Result<GroupState> RemoveStudents(GroupState state, GroupOrderData order)
    {
        List<GroupStudentRow> students = [.. state.Students];
        foreach (GroupOrderStudentLine line in order.Students)
        {
            Result<int> index = RowToEnd(students, line.StudentContractId, order.OrderDate);
            if (index.IsFailure)
            {
                return index.Error;
            }

            students[index.Value] = students[index.Value] with { EndDate = order.OrderDate, Note = line.Note };
        }

        return state with { Students = students };
    }

    /// <summary>
    ///     8 (D189): a-ზე მოქმედი სტრიქონი a-თი სრულდება და a-დან ძველის დასრულებამდე ახალი ტარიფითა და შენიშვნით
    ///     იწყება. თუ ტარიფი არ იცვლება, სტრიქონი არ იყოფა: შენიშვნა მასში ადგილზე იწერება. ამავე წესით ცვლის
    ///     მოსწავლეებს ბრძანება 10, უკვე შეცვლილი განრიგით (D209)
    /// </summary>
    public static Result<GroupState> ChangeStudentFees(GroupState state, GroupOrderData order)
    {
        float weeklyHours = GroupOrderFormulas.WeeklyHours(state.DayTimePlaces, order.OrderDate);
        List<GroupStudentRow> students = [.. state.Students];
        foreach (GroupOrderStudentLine line in order.Students)
        {
            int index = ActiveRowIndex(students, line.StudentContractId, order.OrderDate);
            if (index < 0)
            {
                return GroupOrderErrors.StudentIsNotInGroup;
            }

            GroupStudentRow current = students[index];
            GroupStudentRow changed = line.ToRow(order.OrderDate, current.EndDate, weeklyHours);
            if (!TariffChanges(current, changed))
            {
                students[index] = current with { Note = line.Note };
            }
            else if (StartsOn(current, order.OrderDate))
            {
                return GroupOrderErrors.RowStartsOnOrderDate;
            }
            else
            {
                students[index] = current with { EndDate = order.OrderDate };
                students.Add(changed);
            }
        }

        return state with { Students = students };
    }

    /// <summary>
    ///     5 (D190, D202, D205): „საიდან“ ჯგუფში (source) a-ზე მოქმედი სტრიქონი a-თი სრულდება, „სად“ ჯგუფში (target) კი
    ///     a-დან იწყება ახალი, ხაზის ტარიფითა და შენიშვნით, „საიდან“-ის სტრიქონის დასრულებამდე. პირობები: ორივე ჯგუფი
    ///     a-ზე შექმნილი და ღიაა; ჯგუფები ერთი სასწავლო წლისა და საგნისაა; მოსწავლე „სად“-ში a-ზე უკვე არ არის, როგორც
    ///     დამატებისას (3). null: ჯგუფი ჯერ არ შექმნილა
    /// </summary>
    public static Result<(GroupState Source, GroupState Target)> Transfer(GroupState? source, GroupState? target,
        GroupOrderData order)
    {
        if (source is null)
        {
            return GroupOrderErrors.OrderIsBeforeGroupStart;
        }

        //„საიდან“-ის დახურვა ყველა ბრძანების საერთო წესია (GroupOrderEffects.Apply)
        if (GroupActivity.IsClosedOn(source, order.OrderDate))
        {
            return GroupOrderErrors.GroupIsClosedOnOrderDate;
        }

        if (target is null || GroupActivity.IsClosedOn(target, order.OrderDate))
        {
            return GroupOrderErrors.TargetGroupIsNotOpen;
        }

        if (target.Fields.AcademicYearId != source.Fields.AcademicYearId ||
            target.Fields.CourseId != source.Fields.CourseId)
        {
            return GroupOrderErrors.TargetGroupDoesNotMatch;
        }

        float targetWeeklyHours = GroupOrderFormulas.WeeklyHours(target.DayTimePlaces, order.OrderDate);
        List<GroupStudentRow> sourceStudents = [.. source.Students];
        List<GroupStudentRow> targetStudents = [.. target.Students];
        foreach (GroupOrderStudentLine line in order.Students)
        {
            Result<int> index = RowToEnd(sourceStudents, line.StudentContractId, order.OrderDate);
            if (index.IsFailure)
            {
                return index.Error;
            }

            if (ActiveRowIndex(targetStudents, line.StudentContractId, order.OrderDate) >= 0)
            {
                return GroupOrderErrors.StudentIsAlreadyInGroup;
            }

            GroupStudentRow current = sourceStudents[index.Value];
            sourceStudents[index.Value] = current with { EndDate = order.OrderDate };
            targetStudents.Add(line.ToRow(order.OrderDate, current.EndDate, targetWeeklyHours));
        }

        return (source with { Students = sourceStudents }, target with { Students = targetStudents });
    }

    //a-ზე მოქმედი სტრიქონი, რომელსაც ბრძანება a-თი ასრულებს: ის a-მდე უნდა დაწყებულიყო (D206)
    private static Result<int> RowToEnd(List<GroupStudentRow> students, int studentContractId, DateTime orderDate)
    {
        int index = ActiveRowIndex(students, studentContractId, orderDate);
        if (index < 0)
        {
            return GroupOrderErrors.StudentIsNotInGroup;
        }

        if (StartsOn(students[index], orderDate))
        {
            return GroupOrderErrors.RowStartsOnOrderDate;
        }

        return index;
    }

    //კონტრაქტის სტრიქონი, რომელიც day დღეს მოქმედებს, ან -1
    private static int ActiveRowIndex(List<GroupStudentRow> students, int studentContractId, DateTime day)
    {
        return students.FindIndex(s =>
            s.StudentContractId == studentContractId && GroupActivity.IsRowActiveOn(s.StartDate, s.EndDate, day));
    }

    private static bool StartsOn(GroupStudentRow row, DateTime day)
    {
        return row.StartDate.Date == day.Date;
    }

    //ფასები ზუსტად ედრება, საათები და კოეფიციენტი კი float-ის ზღვრით (D153)
    private static bool TariffChanges(GroupStudentRow current, GroupStudentRow changed)
    {
        return current.OneHourFee != changed.OneHourFee || current.FourWeekFee != changed.FourWeekFee ||
               GeneratorHours.AreDifferent(current.FourWeekHours, changed.FourWeekHours) ||
               GeneratorHours.AreDifferent(current.HoursCoefficient, changed.HoursCoefficient);
    }
}
