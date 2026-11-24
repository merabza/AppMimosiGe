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
        return [.. orders.Select(GroupOrderEntityMapper.ToData)];
    }

    public Task<GroupOrder?> GetOrderForChange(int orderId, CancellationToken cancellationToken = default)
    {
        return context.GroupOrders.Include(o => o.GroupOrderTeachers).Include(o => o.GroupOrderStudents)
            .Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
    }

    public Task<List<Group>> GetGroupsForChange(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        return context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).AsSplitQuery().Where(g => groupIds.Contains(g.GrpId))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> GroupExists(int grpId, CancellationToken cancellationToken = default)
    {
        return context.Groups.AnyAsync(g => g.GrpId == grpId, cancellationToken);
    }

    public async Task<bool> GroupCodeIsTaken(int academicYearId, string groupCode, int? exceptGrpId,
        int? exceptOrderId, CancellationToken cancellationToken = default)
    {
        return await context.Groups.AnyAsync(
                   g => g.AcademicYearId == academicYearId && g.GroupCode == groupCode && g.GrpId != exceptGrpId,
                   cancellationToken) ||
               await context.GroupOrders.AnyAsync(
                   o => o.IsPaused && o.AcademicYearId == academicYearId && o.GroupCode == groupCode &&
                        o.Id != exceptOrderId, cancellationToken);
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

    public async Task<OrderNumber?> GetNumber(int orderId, CancellationToken cancellationToken = default)
    {
        var stored = await context.GroupOrders.AsNoTracking().Where(o => o.Id == orderId)
            .Select(o => new { o.NumberYear, o.Number }).SingleOrDefaultAsync(cancellationToken);
        return stored is { NumberYear: { } numberYear, Number: { } number }
            ? new OrderNumber(numberYear, number)
            : null;
    }

    public void AddOrder(GroupOrder order)
    {
        context.GroupOrders.Add(order);
    }

    public void RemoveOrder(GroupOrder order)
    {
        RemoveLines(new GroupOrderRemovedLines([.. order.GroupOrderTeachers], [.. order.GroupOrderStudents],
            [.. order.GroupOrderDayTimePlaces]));
        context.GroupOrders.Remove(order);
    }

    public void RemoveLines(GroupOrderRemovedLines lines)
    {
        context.GroupOrderTeachers.RemoveRange(lines.Teachers);
        context.GroupOrderStudents.RemoveRange(lines.Students);
        context.GroupOrderDayTimePlaces.RemoveRange(lines.DayTimePlaces);
    }

    public async Task SetOrdersAcademicYear(int groupId, int academicYearId,
        CancellationToken cancellationToken = default)
    {
        foreach (GroupOrder order in await context.GroupOrders
                     .Where(o => o.GroupId == groupId || o.TargetGroupId == groupId).ToListAsync(cancellationToken))
        {
            order.AcademicYearId = academicYearId;
        }
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
    public async Task RemoveGroup(Group group, int? keptOrderId, CancellationToken cancellationToken = default)
    {
        int groupId = group.GrpId;
        context.LessonsByStudents.RemoveRange(await context.LessonsByStudents
            .Where(x => x.Lesson.GroupId == groupId).ToListAsync(cancellationToken));
        context.Lessons.RemoveRange(await context.Lessons.Where(x => x.GroupId == groupId)
            .ToListAsync(cancellationToken));
        context.GroupOrders.RemoveRange(await context.GroupOrders.Include(o => o.GroupOrderTeachers)
            .Include(o => o.GroupOrderStudents).Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery()
            .Where(o => o.GroupId == groupId && o.Id != keptOrderId).ToListAsync(cancellationToken));
        await RemoveRows(new GroupRemovedRows([.. group.GroupsByTeachers], [.. group.GroupsByStudents],
            [.. group.GroupDayTimePlaces]), cancellationToken);
        context.Groups.Remove(group);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x => scIds.Contains(x.ScId)).ToListAsync(cancellationToken);
    }
}
