using System;
using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბაზის ბრძანებები ვიზარდი 2-ის ფაილისთვის (D195): ბრძანებები Id-ის რიგით, ხაზები Id-ის რიგით, ხაზს კი მისი სტრიქონის
///     Id, რომ ფაილში დარჩეს ყველაფერი, რასაც ვიზარდი 2 შლის: ბრძანებები, ხაზები და სტრიქონების კავშირები
/// </summary>
public static class GroupOrdersExportMapper
{
    public static GroupOrdersExport ToExport(IEnumerable<GroupOrder> orders, GroupOrderLinkedRows linkedRows,
        DateTime? transitionDate, string userName, DateTime now)
    {
        Dictionary<int, int> teacherRows =
            linkedRows.Teachers.ToDictionary(r => r.GroupOrderTeacherId!.Value, r => r.Id);
        Dictionary<int, int> studentRows =
            linkedRows.Students.ToDictionary(r => r.GroupOrderStudentId!.Value, r => r.GbsId);
        Dictionary<int, int> dayTimePlaceRows =
            linkedRows.DayTimePlaces.ToDictionary(r => r.GroupOrderDayTimePlaceId!.Value, r => r.GdtpId);
        return new GroupOrdersExport(now, userName, transitionDate,
        [
            .. orders.OrderBy(o => o.Id).Select(o => new GroupOrderExport(o.Id, (int)o.OrderType, o.AcademicYearId,
                o.NumberYear, o.Number, o.IssueDate, o.OrderDate, o.IsPaused, o.GroupId, o.TargetGroupId,
                o.GroupCode, o.CourseId, o.GroupSizeId, o.StudentStatusId, o.VoidDate, o.OrganizationName,
                o.SignerTitle, o.SignerName, o.CreatedByUserName, o.CreatedAt, o.ChangedByUserName, o.ChangedAt,
                [
                    .. o.GroupOrderTeachers.OrderBy(x => x.Id).Select(x => new GroupOrderTeacherExport(x.Id,
                        x.TeacherContractId, x.SalarySchemaId, x.StartDate, x.EndDate, Row(teacherRows, x.Id)))
                ],
                [
                    .. o.GroupOrderStudents.OrderBy(x => x.Id).Select(x => new GroupOrderStudentExport(x.Id,
                        x.StudentContractId, x.FourWeekHours, x.FourWeekFee, x.OneHourFee, x.HoursCoefficient,
                        x.Note, x.StartDate, x.EndDate, Row(studentRows, x.Id)))
                ],
                [
                    .. o.GroupOrderDayTimePlaces.OrderBy(x => x.Id).Select(x => new GroupOrderDayTimePlaceExport(
                        x.Id, x.WeekDayId, x.LessonStartTimeId, x.HoursCount, x.RoomId, x.StartDate, x.EndDate,
                        Row(dayTimePlaceRows, x.Id)))
                ]))
        ]);
    }

    private static int? Row(Dictionary<int, int> rows, int lineId)
    {
        return rows.TryGetValue(lineId, out int rowId) ? rowId : null;
    }
}
