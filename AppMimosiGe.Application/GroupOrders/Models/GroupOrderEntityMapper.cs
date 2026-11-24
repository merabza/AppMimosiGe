using System;
using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანება ბაზაში (GroupOrder ხაზებით) და ძრავში (GroupOrderData). ხაზს ორივეგან ერთი Id აქვს; შესანახი ბრძანების
///     ახალ ხაზს ძრავში დროებითი უარყოფითი Id აქვს, რომლითაც მისი სტრიქონი მას უკავშირდება (GroupOrderNewLines)
/// </summary>
public static class GroupOrderEntityMapper
{
    /// <summary>
    ///     ბაზის ბრძანება ძრავისთვის. ჯგუფის ველები მხოლოდ საწყის და შექმნის ბრძანებას აქვს (D186, D196); ხაზები Id-ის რიგით
    /// </summary>
    public static GroupOrderData ToData(GroupOrder order)
    {
        return new GroupOrderData
        {
            Id = order.Id,
            OrderType = order.OrderType,
            OrderDate = order.OrderDate,
            IsPaused = order.IsPaused,
            GroupId = order.GroupId,
            TargetGroupId = order.TargetGroupId,
            Fields = order.OrderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup
                ? new GroupFields(order.AcademicYearId, order.GroupCode!, order.CourseId!.Value,
                    order.GroupSizeId!.Value, order.StudentStatusId!.Value)
                : null,
            VoidDate = order.VoidDate,
            Teachers =
            [
                .. order.GroupOrderTeachers.OrderBy(x => x.Id).Select(x =>
                    new GroupOrderTeacherLine(x.Id, x.TeacherContractId, x.SalarySchemaId, x.StartDate, x.EndDate))
            ],
            Students =
            [
                .. order.GroupOrderStudents.OrderBy(x => x.Id).Select(x => new GroupOrderStudentLine(x.Id,
                    x.StudentContractId, x.FourWeekHours, x.FourWeekFee, x.OneHourFee, x.HoursCoefficient, x.Note,
                    x.StartDate, x.EndDate))
            ],
            DayTimePlaces =
            [
                .. order.GroupOrderDayTimePlaces.OrderBy(x => x.Id).Select(x => new GroupOrderDayTimePlaceLine(x.Id,
                    x.WeekDayId, x.LessonStartTimeId, x.HoursCount, x.RoomId, x.StartDate, x.EndDate))
            ]
        };
    }

    /// <summary>
    ///     data-ს ჩაწერა ბაზის ბრძანებაში: ტიპი, თარიღი, დაპაუზება, ჯგუფები, ჯგუფის ველები, გაუქმების თარიღი და ხაზები.
    ///     ხაზი Id-ით ედრება: არსებული სწორდება, დროებითი Id-ის ხაზი ემატება, დანარჩენი მოხსნილია. სასწავლო წელს, ნომერს და
    ///     გამოცემის მონაცემებს handler-ი წერს. studentsSchedule: იმ ჯგუფის განრიგი, სადაც მოსწავლეების ხაზები სტრიქონებს
    ///     ქმნის (გადაყვანისას სამიზნე ჯგუფის). ხაზის 4 კვირის საათები ბეჭდვისთვისაა და ძრავის ფორმულით ითვლება ამ
    ///     განრიგით ბრძანების თარიღზე (D187); საწყისი ბრძანების საათები ხაზიდან მოდის
    /// </summary>
    public static (GroupOrderNewLines NewLines, GroupOrderRemovedLines Removed) Write(GroupOrder order,
        GroupOrderData data, IReadOnlyList<GroupDayTimePlaceRow> studentsSchedule)
    {
        order.OrderType = data.OrderType;
        order.OrderDate = data.OrderDate;
        order.IsPaused = data.IsPaused;
        order.GroupId = data.GroupId;
        order.TargetGroupId = data.TargetGroupId;
        order.GroupCode = data.Fields?.GroupCode;
        order.CourseId = data.Fields?.CourseId;
        order.GroupSizeId = data.Fields?.GroupSizeId;
        order.StudentStatusId = data.Fields?.StudentStatusId;
        order.VoidDate = data.VoidDate;

        Dictionary<int, GroupOrderTeacher> newTeachers = [];
        List<GroupOrderTeacher> removedTeachers = WriteLines(order.GroupOrderTeachers, x => x.Id, data.Teachers,
            line => line.Id, newTeachers, (teacher, line) =>
            {
                teacher.TeacherContractId = line.TeacherContractId;
                teacher.SalarySchemaId = line.SalarySchemaId;
                teacher.StartDate = line.StartDate;
                teacher.EndDate = line.EndDate;
            });

        float weeklyHours = GroupOrderFormulas.WeeklyHours(studentsSchedule, data.OrderDate);
        Dictionary<int, GroupOrderStudent> newStudents = [];
        List<GroupOrderStudent> removedStudents = WriteLines(order.GroupOrderStudents, x => x.Id, data.Students,
            line => line.Id, newStudents, (student, line) =>
            {
                student.StudentContractId = line.StudentContractId;
                student.FourWeekHours = FourWeekHours(data.OrderType, line, weeklyHours);
                student.FourWeekFee = line.FourWeekFee;
                student.OneHourFee = line.OneHourFee;
                student.HoursCoefficient = line.HoursCoefficient;
                student.Note = line.Note;
                student.StartDate = line.StartDate;
                student.EndDate = line.EndDate;
            });

        Dictionary<int, GroupOrderDayTimePlace> newDayTimePlaces = [];
        List<GroupOrderDayTimePlace> removedDayTimePlaces = WriteLines(order.GroupOrderDayTimePlaces, x => x.Id,
            data.DayTimePlaces, line => line.Id, newDayTimePlaces, (dayTimePlace, line) =>
            {
                dayTimePlace.WeekDayId = line.WeekDayId;
                dayTimePlace.LessonStartTimeId = line.LessonStartTimeId;
                dayTimePlace.HoursCount = line.HoursCount;
                dayTimePlace.RoomId = line.RoomId;
                dayTimePlace.StartDate = line.StartDate;
                dayTimePlace.EndDate = line.EndDate;
            });

        return (new GroupOrderNewLines(newTeachers, newStudents, newDayTimePlaces),
            new GroupOrderRemovedLines(removedTeachers, removedStudents, removedDayTimePlaces));
    }

    //გაყვანის (4) ხაზს ტარიფი არ აქვს
    private static float? FourWeekHours(EGroupOrderType orderType, GroupOrderStudentLine line, float weeklyHours)
    {
        if (orderType == EGroupOrderType.Initial)
        {
            return line.FourWeekHours;
        }

        return line.HoursCoefficient is { } hoursCoefficient
            ? GroupOrderFormulas.FourWeekHours(hoursCoefficient, weeklyHours)
            : null;
    }

    //ბრძანების ხაზები data-ს ხაზებს Id-ით ედრება: არსებული სწორდება, დანარჩენი ახალია და newLines-ში იწერება მისი
    //დროებითი Id-ით. აბრუნებს ხაზებს, რომლებიც data-ში აღარ არის
    private static List<TEntity> WriteLines<TEntity, TLine>(ICollection<TEntity> entities, Func<TEntity, int> entityId,
        IReadOnlyList<TLine> lines, Func<TLine, int> lineId, Dictionary<int, TEntity> newLines,
        Action<TEntity, TLine> write) where TEntity : new()
    {
        HashSet<int> lineIds = [.. lines.Select(lineId)];
        List<TEntity> removed = [.. entities.Where(x => !lineIds.Contains(entityId(x)))];
        Dictionary<int, TEntity> kept = entities.Except(removed).ToDictionary(entityId);
        foreach (TLine line in lines)
        {
            if (!kept.TryGetValue(lineId(line), out TEntity? entity))
            {
                entity = new TEntity();
                entities.Add(entity);
                newLines[lineId(line)] = entity;
            }

            write(entity, line);
        }

        return removed;
    }
}
