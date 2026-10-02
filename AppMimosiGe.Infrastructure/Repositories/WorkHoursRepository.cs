using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class WorkHoursRepository(IMimosiGeDbContext context) : IWorkHoursRepository
{
    private const int CancelledLessonStatusId = 2;

    //თანამშრომელი "გვარი სახელი / ნომერი"-თ (Access-ის ფორმის ჩამოსაშლელი სია)
    private static readonly Expression<Func<TeacherContract, WorkHourEmployee>> ToEmployee = x =>
        new WorkHourEmployee(x.Id, x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber,
            x.ContractDate, x.ContractEndDate, x.WorkHoursStart, x.WorkHoursEnd);

    //ფილტრი და დალაგება SQL სერვერზე ხდება. ჯამებს ფილტრის ყველა ჩანაწერი სჭირდება (წელიწადში რამდენიმე ასეული),
    //ამიტომ ყველა იტვირთება და გვერდი კოდში იჭრება
    public async Task<WorkHoursRowsDataResponse> GetRowsData(WorkHoursListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WorkHourRowData> rows = ApplyFilter(context.WorkHours.AsNoTracking(), query).Select(w =>
            new WorkHourRowData
            {
                Id = w.WhId,
                TeacherContractId = w.TeacherContractId,
                EmployeeName =
                    w.TeacherContract.TeacherHuman.LastName + " " + w.TeacherContract.TeacherHuman.FirstName +
                    " / " + w.TeacherContract.ContractNumber,
                WhStart = w.WhStart,
                WhEnd = w.WhEnd
            });

        List<WorkHourRowData> all = await ApplySort(rows, query.SortFields).ToListAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= all.Count && all.Count > 0)
        {
            offset = (all.Count - 1) / query.RowsCount * query.RowsCount;
        }

        return new WorkHoursRowsDataResponse(all.Count, offset, [
                .. all.Skip(offset).Take(query.RowsCount).Select(r => new WorkHourRowResponse(r.Id, r.TeacherContractId,
                    r.EmployeeName, r.WhStart, r.WhEnd, WorkHourDurations.Hours(r.WhStart, r.WhEnd)))
            ],
            WorkHourDurations.Totals(all.Select(r =>
                new WorkHourTimeData(r.TeacherContractId, r.EmployeeName, r.WhStart, r.WhEnd))));
    }

    public Task<WorkHourResponse?> GetOne(int whId, CancellationToken cancellationToken = default)
    {
        return context.WorkHours.AsNoTracking().Where(w => w.WhId == whId).Select(w => new WorkHourResponse(w.WhId,
            w.TeacherContractId,
            w.TeacherContract.TeacherHuman.LastName + " " + w.TeacherContract.TeacherHuman.FirstName + " / " +
            w.TeacherContract.ContractNumber, w.WhStart, w.WhEnd)).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<WorkHour?> GetForChange(int whId, CancellationToken cancellationToken = default)
    {
        return context.WorkHours.SingleOrDefaultAsync(w => w.WhId == whId, cancellationToken);
    }

    public Task<bool> EmployeeExists(int teacherContractId, CancellationToken cancellationToken = default)
    {
        return EmployeeContracts().AnyAsync(x => x.Id == teacherContractId, cancellationToken);
    }

    public Task<WorkHourEmployee?> GetEmployee(int teacherContractId, CancellationToken cancellationToken = default)
    {
        return EmployeeContracts().Where(x => x.Id == teacherContractId).Select(ToEmployee)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<List<WorkHourEmployee>> GetEmployees(CancellationToken cancellationToken = default)
    {
        return EmployeeContracts().Select(ToEmployee).ToListAsync(cancellationToken);
    }

    //სახელი კონტრაქტის ნომრით მთავრდება, ნომერი კი უნიკალურია (UQ), ამიტომ სახელით დალაგება ცალსახაა
    public Task<List<LookupItemResponse>> GetEmployeeLookups(CancellationToken cancellationToken = default)
    {
        return EmployeeContracts()
            .Select(x => new
            {
                x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasRecordOnDay(int teacherContractId, DateTime day, CancellationToken cancellationToken = default)
    {
        DateTime dayStart = day.Date;
        DateTime dayAfter = dayStart.AddDays(1);
        return context.WorkHours.AnyAsync(
            w => w.TeacherContractId == teacherContractId && w.WhStart >= dayStart && w.WhStart < dayAfter,
            cancellationToken);
    }

    public Task<WorkHour?> GetLastRecordOfDayForChange(int teacherContractId, DateTime day,
        CancellationToken cancellationToken = default)
    {
        DateTime dayStart = day.Date;
        DateTime dayAfter = dayStart.AddDays(1);
        return context.WorkHours
            .Where(w => w.TeacherContractId == teacherContractId && w.WhStart >= dayStart && w.WhStart < dayAfter)
            .OrderByDescending(w => w.WhStart).ThenByDescending(w => w.WhId).FirstOrDefaultAsync(cancellationToken);
    }

    //Access-ის vCountedWorkHours: Lessons INNER JOIN LessonsByStudents, Status <> 2
    public Task<List<LessonTimeData>> GetLessonTimes(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        return context.LessonsByStudents.AsNoTracking()
            .Where(s => s.Lesson.LessonStatusId != CancelledLessonStatusId && s.Lesson.LessonDt >= from &&
                        s.Lesson.LessonDt < toExclusive)
            .Select(s => new LessonTimeData(s.Lesson.LessonDt, s.HoursCount)).ToListAsync(cancellationToken);
    }

    public async Task<List<WorkHourDay>> GetRecordDays(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        var records = await context.WorkHours.AsNoTracking().Where(w => w.WhStart >= from && w.WhStart < toExclusive)
            .Select(w => new { w.TeacherContractId, w.WhStart }).ToListAsync(cancellationToken);
        return [.. records.Select(r => new WorkHourDay(r.TeacherContractId, r.WhStart.Date))];
    }

    public void Add(WorkHour workHour)
    {
        context.WorkHours.Add(workHour);
    }

    public void Remove(WorkHour workHour)
    {
        context.WorkHours.Remove(workHour);
    }

    //თანამშრომელი სამუშაო საათების ჯგუფის მქონე კონტრაქტია (Access-ის ჩამოსაშლელი სიის WHERE)
    private IQueryable<TeacherContract> EmployeeContracts()
    {
        return context.TeacherContracts.AsNoTracking().Where(x => x.WorkHourGroupId != null);
    }

    //Access-ის ფილტრი: WhEnd >= თარიღიდან და WhStart <= თარიღამდე (დღის ბოლომდე). დაუსრულებელი ჩანაწერი
    //(Access-ისგან განსხვავებით) დაწყებით მოწმდება, რომ დღეს დაწყებული ჩანაწერიც ჩანდეს
    private static IQueryable<WorkHour> ApplyFilter(IQueryable<WorkHour> workHours, WorkHoursListQuery query)
    {
        if (query.TeacherContractId is { } teacherContractId)
        {
            workHours = workHours.Where(w => w.TeacherContractId == teacherContractId);
        }

        if (query.DateFrom is { } dateFrom)
        {
            workHours = workHours.Where(w => (w.WhEnd ?? w.WhStart) >= dateFrom);
        }

        if (query.DateTo is { } dateTo)
        {
            DateTime dayAfter = dateTo.AddDays(1);
            workHours = workHours.Where(w => w.WhStart < dayAfter);
        }

        return workHours;
    }

    //ბოლოს ID-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<WorkHourRowData> ApplySort(IQueryable<WorkHourRowData> query,
        IReadOnlyList<WorkHourSortField> sortFields)
    {
        IOrderedQueryable<WorkHourRowData>? ordered = null;
        foreach (WorkHourSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                EWorkHourSortField.WhStart => Order(query, ordered, r => r.WhStart, sortField.Ascending),
                EWorkHourSortField.WhEnd => Order(query, ordered, r => r.WhEnd, sortField.Ascending),
                EWorkHourSortField.EmployeeName => Order(query, ordered, r => r.EmployeeName, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.Id, true);
    }

    private static IOrderedQueryable<WorkHourRowData> Order<TKey>(IQueryable<WorkHourRowData> query,
        IOrderedQueryable<WorkHourRowData>? ordered, Expression<Func<WorkHourRowData, TKey>> keySelector,
        bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
