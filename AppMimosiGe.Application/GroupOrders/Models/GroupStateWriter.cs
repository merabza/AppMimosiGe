using System;
using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Groups.Models;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის სამიზნე მდგომარეობის ჩაწერა ბაზის ჯგუფში (GROUP_ORDERS.md §5 ნაბიჯი 5, D198). სტრიქონი სამიზნეს ხაზის
///     კავშირით ედრება (GroupOrderTeacherId, GroupOrderStudentId, GroupOrderDayTimePlaceId = სამიზნე სტრიქონის LineId):
///     არსებული სტრიქონი იმავე Id-ით რჩება და მისი ველები და თარიღები სწორდება (მასზეა მიბმული გაკვეთილების მოსწავლეები),
///     ახალი ემატება, დანარჩენი კი მოხსნილია. მოხსნილ სტრიქონს რეპოზიტორია შლის: ჯგუფის კოლექციიდან ამოგდებას EF
///     Restrict კავშირის გაწყვეტად ჩათვლიდა
/// </summary>
public static class GroupStateWriter
{
    /// <summary>
    ///     state-ის ჩაწერა არსებულ ჯგუფში: ველები, გაუქმების თარიღი და სტრიქონები. აბრუნებს მოხსნილ სტრიქონებს
    /// </summary>
    public static GroupRemovedRows Apply(Group group, GroupState state, GroupOrderNewLines newLines)
    {
        group.AcademicYearId = state.Fields.AcademicYearId;
        group.GroupCode = state.Fields.GroupCode;
        group.CourseId = state.Fields.CourseId;
        group.GroupSizeId = state.Fields.GroupSizeId;
        group.StudentStatusId = state.Fields.StudentStatusId;
        group.VoidDate = state.VoidDate;

        List<GroupByTeacher> removedTeachers = WriteRows(group.GroupsByTeachers, x => x.GroupOrderTeacherId,
            state.Teachers, row => row.LineId,
            lineId => newLines.Teachers.TryGetValue(lineId, out GroupOrderTeacher? line)
                ? new GroupByTeacher { GroupOrderTeacher = line }
                : new GroupByTeacher { GroupOrderTeacherId = lineId }, (teacher, row) =>
            {
                teacher.TeacherContractId = row.TeacherContractId;
                teacher.SalarySchemaId = row.SalarySchemaId;
                teacher.StartDate = row.StartDate;
                teacher.EndDate = row.EndDate;
            });

        List<GroupByStudent> removedStudents = WriteRows(group.GroupsByStudents, x => x.GroupOrderStudentId,
            state.Students, row => row.LineId,
            lineId => newLines.Students.TryGetValue(lineId, out GroupOrderStudent? line)
                ? new GroupByStudent { GroupOrderStudent = line }
                : new GroupByStudent { GroupOrderStudentId = lineId }, (student, row) =>
            {
                student.StudentContractId = row.StudentContractId;
                student.FourWeekHours = row.FourWeekHours;
                student.FourWeekFee = row.FourWeekFee;
                student.OneHourFee = row.OneHourFee;
                student.HoursCoefficient = row.HoursCoefficient;
                student.Note = row.Note;
                student.StartDate = row.StartDate;
                student.EndDate = row.EndDate;
            });

        List<GroupDayTimePlace> removedDayTimePlaces = WriteRows(group.GroupDayTimePlaces,
            x => x.GroupOrderDayTimePlaceId, state.DayTimePlaces, row => row.LineId,
            lineId => newLines.DayTimePlaces.TryGetValue(lineId, out GroupOrderDayTimePlace? line)
                ? new GroupDayTimePlace { GroupOrderDayTimePlace = line }
                : new GroupDayTimePlace { GroupOrderDayTimePlaceId = lineId }, (dayTimePlace, row) =>
            {
                dayTimePlace.WeekDayId = row.WeekDayId;
                dayTimePlace.LessonStartTimeId = row.LessonStartTimeId;
                dayTimePlace.HoursCount = row.HoursCount;
                dayTimePlace.RoomId = row.RoomId;
                dayTimePlace.StartDate = row.StartDate;
                dayTimePlace.EndDate = row.EndDate;
            });

        return new GroupRemovedRows(removedTeachers, removedStudents, removedDayTimePlaces);
    }

    /// <summary>
    ///     ახალი ჯგუფი state-იდან (შექმნის ბრძანება, D186): ყველა სტრიქონი ახალია და თავის ხაზს უკავშირდება
    /// </summary>
    public static Group Create(GroupState state, GroupOrderNewLines newLines)
    {
        var group = new Group { GroupCode = state.Fields.GroupCode };
        Apply(group, state, newLines);
        return group;
    }

    //ჯგუფის სტრიქონები სამიზნეს ხაზის კავშირით ედრება: არსებული სწორდება, ახალი ემატება. აბრუნებს მოხსნილებს: მათ,
    //რომელთა ხაზიც სამიზნეში აღარ არის, და კავშირის გარეშე სტრიქონებს
    private static List<TEntity> WriteRows<TEntity, TRow>(ICollection<TEntity> entities,
        Func<TEntity, int?> entityLineId, IReadOnlyList<TRow> rows, Func<TRow, int> rowLineId,
        Func<int, TEntity> create, Action<TEntity, TRow> write)
    {
        HashSet<int> lineIds = [.. rows.Select(rowLineId)];
        List<TEntity> removed = [.. entities.Where(x => entityLineId(x) is not { } lineId || !lineIds.Contains(lineId))];
        Dictionary<int, TEntity> kept = entities.Except(removed).ToDictionary(x => entityLineId(x)!.Value);
        foreach (TRow row in rows)
        {
            if (!kept.TryGetValue(rowLineId(row), out TEntity? entity))
            {
                entity = create(rowLineId(row));
                entities.Add(entity);
            }

            write(entity, row);
        }

        return removed;
    }
}
