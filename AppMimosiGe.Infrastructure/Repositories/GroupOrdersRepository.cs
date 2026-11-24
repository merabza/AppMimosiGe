using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<GroupOrdersRowsDataResponse> GetRowsData(GroupOrdersListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<GroupOrderRowData> rows = CreateRows(query);

        int count = await rows.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        List<GroupOrderRowData> page = await ApplySort(rows, query.SortFields).Skip(offset).Take(query.RowsCount)
            .ToListAsync(cancellationToken);

        return new GroupOrdersRowsDataResponse(count, offset, [
            .. page.Select(r => new GroupOrderRowResponse(r.Id, (int)r.OrderType, NumberText(r.NumberYear, r.Number),
                r.IssueDate, r.OrderDate, r.IsPaused, r.AcademicYearId, r.AcademicYearName, r.GroupId,
                r.GroupCode ?? string.Empty, r.TargetGroupId, r.TargetGroupCode))
        ]);
    }

    //მოსწავლეები „გვარი სახელი / ნომერი"-თ და ამ სახელით ლაგდება, როგორც ჯგუფის ფორმაში
    public async Task<GroupOrderResponse?> GetOne(int orderId, CancellationToken cancellationToken = default)
    {
        var order = await context.GroupOrders.AsNoTracking().Where(o => o.Id == orderId).Select(o => new
        {
            Order = o,
            Teachers = o.GroupOrderTeachers.OrderBy(t => t.StartDate).ThenBy(t => t.Id)
                .Select(t => new GroupOrderTeacherResponse(t.Id, t.TeacherContractId, t.SalarySchemaId, t.StartDate,
                    t.EndDate)).ToList(),
            Students = o.GroupOrderStudents
                .OrderBy(s => s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName +
                              " / " + s.StudentContract.ContractNumber).ThenBy(s => s.StartDate).ThenBy(s => s.Id)
                .Select(s => new GroupOrderStudentResponse(s.Id, s.StudentContractId,
                    s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName + " / " +
                    s.StudentContract.ContractNumber, s.FourWeekHours, s.FourWeekFee, s.OneHourFee, s.HoursCoefficient,
                    s.Note, s.StartDate, s.EndDate)).ToList(),
            DayTimePlaces = o.GroupOrderDayTimePlaces.OrderBy(d => d.StartDate).ThenBy(d => d.Id)
                .Select(d => new GroupOrderDayTimePlaceResponse(d.Id, d.WeekDayId, d.LessonStartTimeId, d.HoursCount,
                    d.RoomId, d.StartDate, d.EndDate)).ToList()
        }).SingleOrDefaultAsync(cancellationToken);
        if (order is null)
        {
            return null;
        }

        GroupOrder o = order.Order;
        return new GroupOrderResponse(o.Id, (int)o.OrderType, NumberText(o.NumberYear, o.Number), o.IssueDate,
            o.OrderDate, o.IsPaused, o.AcademicYearId, o.GroupId, o.TargetGroupId, o.GroupCode, o.CourseId,
            o.GroupSizeId, o.StudentStatusId, o.VoidDate, o.OrganizationName, o.SignerTitle, o.SignerName,
            o.CreatedByUserName, o.CreatedAt, o.ChangedByUserName, o.ChangedAt, order.Teachers, order.Students,
            order.DayTimePlaces);
    }

    public async Task<GroupOrderData?> GetOrder(int orderId, CancellationToken cancellationToken = default)
    {
        GroupOrder? order = await context.GroupOrders.AsNoTracking().Include(o => o.GroupOrderTeachers)
            .Include(o => o.GroupOrderStudents).Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery()
            .SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        return order is null ? null : GroupOrderEntityMapper.ToData(order);
    }

    //ჯგუფის ველების სახელები ჯგუფიდან მოდის, დაპაუზებულ შექმნის ბრძანებას კი, რომელსაც ჯგუფი ჯერ არ აქვს, საკუთარი
    //ველებიდან. ზომა „ადგილები-დასახელება", როგორც ჯგუფის ფორმაში
    public async Task<GroupOrderPrintSource?> GetPrintSource(int orderId,
        CancellationToken cancellationToken = default)
    {
        GroupOrderData? order = await GetOrder(orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var header = await context.GroupOrders.AsNoTracking().Where(o => o.Id == orderId).Select(o => new
        {
            o.NumberYear,
            o.Number,
            o.IssueDate,
            o.OrganizationName,
            o.SignerTitle,
            o.SignerName,
            GroupCode = o.Group != null ? o.Group.GroupCode : o.GroupCode,
            TargetGroupCode = o.TargetGroup != null ? o.TargetGroup.GroupCode : null,
            o.AcademicYear.AcademicYearName,
            CourseName = o.Group != null ? o.Group.Course.CourseName : o.Course!.CourseName,
            Size = o.Group != null ? o.Group.GroupSize.GrsSize : o.GroupSize!.GrsSize,
            SizeName = o.Group != null ? o.Group.GroupSize.GrsName : o.GroupSize!.GrsName,
            StudentStatusName = o.Group != null
                ? o.Group.StudentStatus.StudentStatusName
                : o.StudentStatus!.StudentStatusName
        }).SingleAsync(cancellationToken);

        return new GroupOrderPrintSource(order,
            header is { NumberYear: { } numberYear, Number: { } number } ? new OrderNumber(numberYear, number) : null,
            header.IssueDate, header.OrganizationName, header.SignerTitle, header.SignerName,
            header.GroupCode ?? string.Empty, header.TargetGroupCode, header.AcademicYearName, header.CourseName,
            $"{header.Size}-{header.SizeName}", header.StudentStatusName);
    }

    //ToDictionaryAsync-ის სელექტორები ჩატვირთვის შემდეგ მუშაობს, ამიტომ სახელი ჯერ SQL-ში იკრიბება. სქემები, დღეები,
    //დროები და ოთახები პატარა ცნობარებია და მთლიანად იტვირთება
    public async Task<GroupOrderPrintNames> GetPrintNames(IReadOnlyCollection<int> teacherContractIds,
        IReadOnlyCollection<int> studentContractIds, CancellationToken cancellationToken = default)
    {
        Dictionary<int, string> teachers = await context.TeacherContracts.AsNoTracking()
            .Where(x => teacherContractIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        Dictionary<int, string> students = await context.StudentContracts.AsNoTracking()
            .Where(x => studentContractIds.Contains(x.ScId))
            .Select(x => new { x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName })
            .ToDictionaryAsync(x => x.ScId, x => x.Name, cancellationToken);
        Dictionary<int, string> salarySchemes = await context.TeacherSalarySchemes.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.SchemaName, cancellationToken);
        Dictionary<int, string> weekDays =
            await context.WeekDays.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        //სახელი "HH:mm" (LessonStartTime.Name) მხოლოდ ჩატვირთვის შემდეგ იქმნება
        Dictionary<int, string> lessonStartTimes =
            (await context.LessonStartTimes.AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(x => x.LstId,
                x => x.Name);
        Dictionary<int, string> rooms =
            await context.Rooms.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.RoomName, cancellationToken);
        return new GroupOrderPrintNames(teachers, salarySchemes, students, weekDays, lessonStartTimes, rooms);
    }

    public async Task<Dictionary<int, GroupState>> GetGroupStates(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default)
    {
        List<Group> groups = await context.Groups.AsNoTracking().Include(g => g.GroupsByTeachers)
            .Include(g => g.GroupsByStudents).Include(g => g.GroupDayTimePlaces).AsSplitQuery()
            .Where(g => groupIds.Contains(g.GrpId)).ToListAsync(cancellationToken);
        return groups.ToDictionary(g => g.GrpId, GroupStateMapper.ToState);
    }

    public Task<List<GroupOrderGroupLookupResponse>> GetGroupLookups(int academicYearId,
        CancellationToken cancellationToken = default)
    {
        return context.Groups.AsNoTracking().Where(g => g.AcademicYearId == academicYearId)
            .OrderBy(g => g.GroupCode).ThenBy(g => g.GrpId).Select(g => new GroupOrderGroupLookupResponse(g.GrpId,
                g.GroupCode, g.AcademicYearId, g.CourseId, g.GroupSizeId, g.VoidDate,
                context.GroupOrders.Any(o => o.GroupId == g.GrpId && !o.IsPaused &&
                                             (o.OrderType == EGroupOrderType.Initial ||
                                              o.OrderType == EGroupOrderType.CreateGroup))))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetTakenGroupCodes(int academicYearId, int? exceptGrpId, int? exceptOrderId,
        CancellationToken cancellationToken = default)
    {
        List<string> codes = await context.Groups.AsNoTracking()
            .Where(g => g.AcademicYearId == academicYearId && g.GrpId != exceptGrpId).Select(g => g.GroupCode)
            .ToListAsync(cancellationToken);
        codes.AddRange(await context.GroupOrders.AsNoTracking()
            .Where(o => o.IsPaused && o.AcademicYearId == academicYearId && o.GroupCode != null &&
                        o.Id != exceptOrderId).Select(o => o.GroupCode!).ToListAsync(cancellationToken));
        return codes;
    }

    public Task<int?> GetStudentStatusRate(int studentStatusId, CancellationToken cancellationToken = default)
    {
        return context.StudentStatuses.AsNoTracking().Where(x => x.Id == studentStatusId).Select(x => (int?)x.Rate)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<List<Group>> GetAllGroupsForChange(CancellationToken cancellationToken = default)
    {
        return context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).AsSplitQuery().ToListAsync(cancellationToken);
    }

    public Task<GroupOrdersMode?> GetModeForChange(CancellationToken cancellationToken = default)
    {
        return context.GroupOrdersMode.SingleOrDefaultAsync(x => x.Id == GroupOrdersMode.SingleRowId,
            cancellationToken);
    }

    public void AddMode(GroupOrdersMode mode)
    {
        context.GroupOrdersMode.Add(mode);
    }

    public Task<List<GroupOrder>> GetAllOrdersForChange(CancellationToken cancellationToken = default)
    {
        return context.GroupOrders.Include(o => o.GroupOrderTeachers).Include(o => o.GroupOrderStudents)
            .Include(o => o.GroupOrderDayTimePlaces).AsSplitQuery().OrderBy(o => o.Id).ToListAsync(cancellationToken);
    }

    public async Task<GroupOrderLinkedRows> GetLinkedRowsForChange(CancellationToken cancellationToken = default)
    {
        return new GroupOrderLinkedRows(
            await context.GroupsByTeachers.Where(x => x.GroupOrderTeacherId != null).ToListAsync(cancellationToken),
            await context.GroupsByStudents.Where(x => x.GroupOrderStudentId != null).ToListAsync(cancellationToken),
            await context.GroupDayTimePlaces.Where(x => x.GroupOrderDayTimePlaceId != null)
                .ToListAsync(cancellationToken));
    }

    private IQueryable<GroupOrderRowData> CreateRows(GroupOrdersListQuery query)
    {
        IQueryable<GroupOrder> orders = context.GroupOrders.AsNoTracking();
        if (query.AcademicYearId is { } academicYearId)
        {
            orders = orders.Where(o => o.AcademicYearId == academicYearId);
        }

        if (query.OrderType is { } orderType)
        {
            orders = orders.Where(o => o.OrderType == orderType);
        }

        //გადაყვანა ორივე ჯგუფის ბრძანებაა
        if (query.GroupId is { } groupId)
        {
            orders = orders.Where(o => o.GroupId == groupId || o.TargetGroupId == groupId);
        }

        if (query.DateFrom is { } dateFrom)
        {
            orders = orders.Where(o => o.OrderDate >= dateFrom);
        }

        //DateTo ჩათვლით (D216)
        if (query.DateTo is { } dateTo)
        {
            DateTime dayAfter = dateTo.AddDays(1);
            orders = orders.Where(o => o.OrderDate < dayAfter);
        }

        if (query.IsPaused is { } isPaused)
        {
            orders = orders.Where(o => o.IsPaused == isPaused);
        }

        return orders.Select(o => new GroupOrderRowData
        {
            Id = o.Id,
            OrderType = o.OrderType,
            NumberYear = o.NumberYear,
            Number = o.Number,
            IssueDate = o.IssueDate,
            OrderDate = o.OrderDate,
            IsPaused = o.IsPaused,
            AcademicYearId = o.AcademicYearId,
            AcademicYearName = o.AcademicYear.AcademicYearName,
            GroupId = o.GroupId,
            GroupCode = o.Group != null ? o.Group.GroupCode : o.GroupCode,
            TargetGroupId = o.TargetGroupId,
            TargetGroupCode = o.TargetGroup != null ? o.TargetGroup.GroupCode : null
        });
    }

    //ნომერი რიცხვებით: ჯერ წელი, მერე რიგითი ნომერი (D192). ბოლოს Id-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც
    //მდგრადი იყოს
    private static IOrderedQueryable<GroupOrderRowData> ApplySort(IQueryable<GroupOrderRowData> query,
        IReadOnlyList<GroupOrderSortField> sortFields)
    {
        IOrderedQueryable<GroupOrderRowData>? ordered = null;
        foreach (GroupOrderSortField sortField in sortFields)
        {
            bool ascending = sortField.Ascending;
            ordered = sortField.Field switch
            {
                EGroupOrderSortField.Number => Order(query, Order(query, ordered, r => r.NumberYear, ascending),
                    r => r.Number, ascending),
                EGroupOrderSortField.OrderType => Order(query, ordered, r => r.OrderType, ascending),
                EGroupOrderSortField.IssueDate => Order(query, ordered, r => r.IssueDate, ascending),
                EGroupOrderSortField.OrderDate => Order(query, ordered, r => r.OrderDate, ascending),
                EGroupOrderSortField.IsPaused => Order(query, ordered, r => r.IsPaused, ascending),
                EGroupOrderSortField.AcademicYearName => Order(query, ordered, r => r.AcademicYearName, ascending),
                EGroupOrderSortField.GroupCode => Order(query, ordered, r => r.GroupCode, ascending),
                EGroupOrderSortField.TargetGroupCode => Order(query, ordered, r => r.TargetGroupCode, ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field,
                    "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.Id, true);
    }

    private static IOrderedQueryable<GroupOrderRowData> Order<TKey>(IQueryable<GroupOrderRowData> query,
        IOrderedQueryable<GroupOrderRowData>? ordered, Expression<Func<GroupOrderRowData, TKey>> keySelector,
        bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }

    private static string? NumberText(int? numberYear, int? number)
    {
        return numberYear is { } year && number is { } n ? new OrderNumber(year, n).ToString() : null;
    }
}
