using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class GroupOrdersRepository(IMimosiGeDbContext context) : IGroupOrdersRepository
{
    //ხაზები ცალ-ცალკე მოთხოვნებით (AsSplitQuery), რომ ერთმანეთზე არ გამრავლდეს
    public async Task<List<GroupOrderData>> GetOrders(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        List<GroupOrder> orders = await context.GroupOrders.AsNoTracking()
            .Include(o => o.GroupOrderTeachers).Include(o => o.GroupOrderStudents)
            .Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery()
            .Where(o => o.GroupId != null && groupIds.Contains(o.GroupId.Value) ||
                        o.TargetGroupId != null && groupIds.Contains(o.TargetGroupId.Value))
            .OrderBy(o => o.OrderDate).ThenBy(o => o.Id).ToListAsync(cancellationToken);
        return [.. orders.Select(ToData)];
    }

    public Task<List<Group>> GetGroupsForChange(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        return context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).AsSplitQuery().Where(g => groupIds.Contains(g.GrpId))
            .ToListAsync(cancellationToken);
    }

    public async Task<GroupOrdersModeResponse> GetMode(CancellationToken cancellationToken = default)
    {
        return await context.GroupOrdersMode.AsNoTracking().Where(x => x.Id == GroupOrdersMode.SingleRowId)
                   .Select(x => new GroupOrdersModeResponse(x.IsOrdersMode, x.TransitionDate, x.SwitchedByUserName,
                       x.SwitchedAt)).SingleOrDefaultAsync(cancellationToken) ??
               new GroupOrdersModeResponse(false, null, null, null);
    }

    public async Task<OrderNumber> NextNumber(int numberYear, CancellationToken cancellationToken = default)
    {
        //Find ჯერ კონტექსტში უკვე არსებულ მთვლელს ეძებს: ერთ კონტექსტში მეორე ნომერი პირველს მოსდევს
        GroupOrderNumber? counter = await context.GroupOrderNumbers.FindAsync([numberYear], cancellationToken);
        OrderNumber number = OrderNumber.Next(numberYear, counter?.LastNumber);
        if (counter is null)
        {
            context.GroupOrderNumbers.Add(new GroupOrderNumber { NumberYear = numberYear, LastNumber = number.Number });
        }
        else
        {
            counter.LastNumber = number.Number;
        }

        return number;
    }

    public void AddGroup(Group group)
    {
        context.Groups.Add(group);
    }

    public async Task RemoveRows(GroupRemovedRows rows, CancellationToken cancellationToken = default)
    {
        int[] gbsIds = [.. rows.Students.Select(x => x.GbsId)];
        if (gbsIds.Length > 0)
        {
            context.LessonsByStudents.RemoveRange(await context.LessonsByStudents
                .Where(x => x.GroupByStudentId != null && gbsIds.Contains(x.GroupByStudentId.Value))
                .ToListAsync(cancellationToken));
        }

        context.GroupsByTeachers.RemoveRange(rows.Teachers);
        context.GroupsByStudents.RemoveRange(rows.Students);
        context.GroupDayTimePlaces.RemoveRange(rows.DayTimePlaces);
    }

    public Task<bool> HasSalary(int groupId, CancellationToken cancellationToken = default)
    {
        return context.SalaryLinesDetails.AnyAsync(x => x.GroupId == groupId, cancellationToken);
    }

    //ყველაფერი, რაც ჯგუფს Restrict-ით ებმის, კონტექსტში იტვირთება და იშლება: ბრძანებების ხაზებიც, რომ EF-მა სტრიქონები
    //მათზე ადრე წაშალოს. ჯგუფის სტრიქონებზე მიბმულ გაკვეთილების მოსწავლეებს RemoveRows შლის, აქ კი ჯგუფის
    //გაკვეთილების ყველა მოსწავლე იშლება. გენერატორის ჟურნალს ბაზა თვითონ შლის (Cascade)
    public async Task RemoveGroup(Group group, CancellationToken cancellationToken = default)
    {
        int groupId = group.GrpId;
        context.LessonsByStudents.RemoveRange(await context.LessonsByStudents
            .Where(x => x.Lesson.GroupId == groupId).ToListAsync(cancellationToken));
        context.Lessons.RemoveRange(await context.Lessons.Where(x => x.GroupId == groupId)
            .ToListAsync(cancellationToken));
        context.GroupOrders.RemoveRange(await context.GroupOrders.Include(o => o.GroupOrderTeachers)
            .Include(o => o.GroupOrderStudents).Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery()
            .Where(o => o.GroupId == groupId).ToListAsync(cancellationToken));
        await RemoveRows(new GroupRemovedRows([.. group.GroupsByTeachers], [.. group.GroupsByStudents],
            [.. group.GroupDayTimePlaces]), cancellationToken);
        context.Groups.Remove(group);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x => scIds.Contains(x.ScId)).ToListAsync(cancellationToken);
    }

    //ჯგუფის ველები მხოლოდ საწყის და შექმნის ბრძანებას აქვს (D186, D196); ხაზები Id-ის რიგით
    private static GroupOrderData ToData(GroupOrder order)
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
}
