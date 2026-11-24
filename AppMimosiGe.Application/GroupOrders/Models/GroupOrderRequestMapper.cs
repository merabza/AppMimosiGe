using System.Collections.Generic;
using System.Linq;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების მოთხოვნა ძრავისთვის (GroupOrderData). მოთხოვნის ახალ ხაზს (Id 0) დროებითი უარყოფითი Id ეძლევა, ხაზის
///     ტიპის შიგნით უნიკალური: სტრიქონის გასაღები ხაზის Id-ია (GroupState), ბაზის ხაზების Id-ები კი დადებითია
/// </summary>
public static class GroupOrderRequestMapper
{
    /// <summary>
    ///     შექმნის ბრძანება მასწავლებლის ან განრიგის გარეშე დაპაუზებულია: ჯგუფს ჯერ არ ქმნის (D186)
    /// </summary>
    public static bool IsPaused(GroupOrderRequest request)
    {
        return request.OrderType == (int)EGroupOrderType.CreateGroup &&
               (request.Teachers.Count == 0 || request.DayTimePlaces.Count == 0);
    }

    /// <summary>
    ///     მოთხოვნის ყოველი არსებული ხაზი (Id != 0) ამ ბრძანებას ეკუთვნის და ერთხელ გვხვდება. order null: ახალი ბრძანება,
    ///     მისი ყველა ხაზი ახალია
    /// </summary>
    public static bool LinesBelongToOrder(GroupOrderRequest request, GroupOrder? order)
    {
        return AllKnown(request.Teachers.Select(x => x.Id), order?.GroupOrderTeachers.Select(x => x.Id)) &&
               AllKnown(request.Students.Select(x => x.Id), order?.GroupOrderStudents.Select(x => x.Id)) &&
               AllKnown(request.DayTimePlaces.Select(x => x.Id), order?.GroupOrderDayTimePlaces.Select(x => x.Id));
    }

    /// <summary>
    ///     ბრძანება მოთხოვნიდან. orderId: ბრძანების Id (ახალ ბრძანებას ისეთი, რომ ერთი დღის ბრძანებებში ბოლო იყოს). groupId:
    ///     ბრძანების ჯგუფი; საწყისი და შექმნის ბრძანების ჯგუფი მოთხოვნაში არ არის. defaultSalarySchemes: სქემის გარეშე
    ///     მოსული მასწავლებლის კონტრაქტის ძირითადი სქემა (D187, კონტრაქტის Id -> სქემა). ჯგუფის კოდი და შენიშვნები
    ///     იწმინდება, თარიღებს დრო ეჭრება
    /// </summary>
    public static GroupOrderData ToData(GroupOrderRequest request, int orderId, int? groupId,
        IReadOnlyDictionary<int, int> defaultSalarySchemes)
    {
        var orderType = (EGroupOrderType)request.OrderType;
        return new GroupOrderData
        {
            Id = orderId,
            OrderType = orderType,
            OrderDate = request.OrderDate.Date,
            IsPaused = IsPaused(request),
            GroupId = groupId,
            TargetGroupId = request.TargetGroupId,
            Fields = orderType is EGroupOrderType.Initial or EGroupOrderType.CreateGroup
                ? new GroupFields(request.AcademicYearId!.Value, request.GroupCode!.Trim(), request.CourseId!.Value,
                    request.GroupSizeId!.Value, request.StudentStatusId!.Value)
                : null,
            VoidDate = request.VoidDate?.Date,
            Teachers =
            [
                .. request.Teachers.Select((x, i) => new GroupOrderTeacherLine(LineId(x.Id, i), x.TeacherContractId,
                    x.SalarySchemaId ?? defaultSalarySchemes[x.TeacherContractId], x.StartDate?.Date, x.EndDate?.Date))
            ],
            Students =
            [
                .. request.Students.Select((x, i) => new GroupOrderStudentLine(LineId(x.Id, i), x.StudentContractId,
                    x.FourWeekHours, x.FourWeekFee, x.OneHourFee, x.HoursCoefficient,
                    string.IsNullOrWhiteSpace(x.Note) ? null : x.Note.Trim(), x.StartDate?.Date, x.EndDate?.Date))
            ],
            DayTimePlaces =
            [
                .. request.DayTimePlaces.Select((x, i) => new GroupOrderDayTimePlaceLine(LineId(x.Id, i), x.WeekDayId,
                    x.LessonStartTimeId, x.HoursCount, x.RoomId, x.StartDate?.Date, x.EndDate?.Date))
            ]
        };
    }

    //ახალი ხაზის (Id 0) დროებითი Id მისი რიგითი ნომრიდან: -1, -2, …
    private static int LineId(int id, int index)
    {
        return id != 0 ? id : -(index + 1);
    }

    private static bool AllKnown(IEnumerable<int> requestedIds, IEnumerable<int>? existingIds)
    {
        HashSet<int> existing = [.. existingIds ?? []];
        List<int> known = [.. requestedIds.Where(id => id != 0)];
        return known.TrueForAll(existing.Contains) && known.Distinct().Count() == known.Count;
    }
}
