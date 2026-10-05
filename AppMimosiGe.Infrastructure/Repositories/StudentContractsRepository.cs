using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class StudentContractsRepository(IMimosiGeDbContext context) : IStudentContractsRepository
{
    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<StudentContractsRowsDataResponse> GetRowsData(StudentContractsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StudentContract> filtered = ApplyFilter(context.StudentContracts.AsNoTracking(), query);

        int count = await filtered.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        IOrderedQueryable<StudentContract> sorted = ApplySort(filtered, query.SortFields);

        List<StudentContractRowResponse> rows = await sorted.Skip(offset).Take(query.RowsCount).Select(sc =>
                new StudentContractRowResponse(sc.ScId, sc.ContractNumber, sc.ContractDate, sc.StudentHumanId,
                    sc.StudentHuman.LastName + " " + sc.StudentHuman.FirstName, sc.PayerHumanId,
                    sc.PayerHuman.LastName + " " + sc.PayerHuman.FirstName, sc.AcademicYearId,
                    sc.AcademicYear.AcademicYearName, sc.StudentStatusId,
                    sc.StudentStatus == null ? null : sc.StudentStatus.StudentStatusName, sc.DesiredMonthlyPaymentDay))
            .ToListAsync(cancellationToken);

        return new StudentContractsRowsDataResponse(count, offset, rows);
    }

    public Task<StudentContractResponse?> GetOne(int scId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(sc => sc.ScId == scId).Select(sc =>
            new StudentContractResponse(sc.ScId, sc.ContractNumber, sc.ContractDate, sc.StudentHumanId,
                sc.StudentHuman.LastName + " " + sc.StudentHuman.FirstName, sc.PayerHumanId,
                sc.PayerHuman.LastName + " " + sc.PayerHuman.FirstName, sc.AcademicYearId, sc.StudentStatusId,
                sc.DesiredMonthlyPaymentDay, sc.NextPayDate, sc.DirtyNextPayDate,
                sc.StudentContractDetails.OrderBy(d => d.Id).Select(d =>
                    new StudentContractDetailResponse(d.Id, d.CourseId, d.GroupSizeId, d.FourWeekHours, d.FourWeekFee,
                        d.OneHourFee)).ToList())).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<StudentContract?> GetForChange(int scId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Include(sc => sc.StudentContractDetails)
            .SingleOrDefaultAsync(sc => sc.ScId == scId, cancellationToken);
    }

    public Task<bool> ContractNumberExists(int academicYearId, string contractNumber, int exceptScId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AnyAsync(
            sc => sc.AcademicYearId == academicYearId && sc.ContractNumber == contractNumber && sc.ScId != exceptScId,
            cancellationToken);
    }

    public Task<bool> StudentHasContract(int academicYearId, int studentHumanId, int exceptScId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AnyAsync(
            sc => sc.AcademicYearId == academicYearId && sc.StudentHumanId == studentHumanId && sc.ScId != exceptScId,
            cancellationToken);
    }

    public Task<List<string>> GetContractNumbers(int academicYearId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(sc => sc.AcademicYearId == academicYearId)
            .Select(sc => sc.ContractNumber).ToListAsync(cancellationToken);
    }

    public Task<AcademicYear?> GetAcademicYear(int ayId, CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AsNoTracking().SingleOrDefaultAsync(ay => ay.AyId == ayId, cancellationToken);
    }

    public async Task<bool> IsInUse(int scId, CancellationToken cancellationToken = default)
    {
        return await context.GroupsByStudents.AnyAsync(x => x.StudentContractId == scId, cancellationToken) ||
               await context.LessonsByStudents.AnyAsync(x => x.StudentContractId == scId, cancellationToken) ||
               await context.Payments.AnyAsync(x => x.StudentContractId == scId, cancellationToken) ||
               await context.CrmCalls.AnyAsync(x => x.StudentContractId == scId, cancellationToken);
    }

    public Task<bool> HumanExists(int humId, CancellationToken cancellationToken = default)
    {
        return context.Humans.AnyAsync(x => x.HumId == humId, cancellationToken);
    }

    public Task<bool> AcademicYearExists(int ayId, CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AnyAsync(x => x.AyId == ayId, cancellationToken);
    }

    public Task<bool> StudentStatusExists(int studentStatusId, CancellationToken cancellationToken = default)
    {
        return context.StudentStatuses.AnyAsync(x => x.Id == studentStatusId, cancellationToken);
    }

    public Task<bool> CourseExists(int crsId, CancellationToken cancellationToken = default)
    {
        return context.Courses.AnyAsync(x => x.CrsId == crsId, cancellationToken);
    }

    public Task<bool> GroupSizeExists(int grsId, CancellationToken cancellationToken = default)
    {
        return context.GroupSizes.AnyAsync(x => x.GrsId == grsId, cancellationToken);
    }

    public Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetStudentStatuses(CancellationToken cancellationToken = default)
    {
        return context.StudentStatuses.AsNoTracking().OrderBy(x => x.Rate).ThenBy(x => x.Id)
            .Select(x => new LookupItemResponse(x.Id, x.StudentStatusName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetCourses(CancellationToken cancellationToken = default)
    {
        return context.Courses.AsNoTracking().OrderBy(x => x.CourseName)
            .Select(x => new LookupItemResponse(x.CrsId, x.CourseName)).ToListAsync(cancellationToken);
    }

    public async Task<List<LookupItemResponse>> GetGroupSizes(CancellationToken cancellationToken = default)
    {
        //Access-ში: Str(Size) + "-" + Name, დალაგების გარეშე (ID-ის რიგით)
        return
        [
            .. (await context.GroupSizes.AsNoTracking().OrderBy(x => x.GrsId).ToListAsync(cancellationToken))
            .Select(x => new LookupItemResponse(x.GrsId, $"{x.GrsSize}-{x.GrsName}"))
        ];
    }

    //გვარ-სახელით (ნებისმიერი რიგით) ან პირადი ნომრის დასაწყისით
    public Task<List<LookupItemResponse>> SearchHumans(string search, int maxCount,
        CancellationToken cancellationToken = default)
    {
        return context.Humans.AsNoTracking()
            .Where(h => (h.LastName + " " + h.FirstName).Contains(search) ||
                        (h.FirstName + " " + h.LastName).Contains(search) || h.PersonalId.StartsWith(search))
            .OrderBy(h => h.LastName).ThenBy(h => h.FirstName).ThenBy(h => h.HumId).Take(maxCount)
            .Select(h => new LookupItemResponse(h.HumId, h.LastName + " " + h.FirstName))
            .ToListAsync(cancellationToken);
    }

    public void Add(StudentContract studentContract)
    {
        context.StudentContracts.Add(studentContract);
    }

    public void Remove(StudentContract studentContract)
    {
        context.StudentContracts.Remove(studentContract);
    }

    private static IQueryable<StudentContract> ApplyFilter(IQueryable<StudentContract> query,
        StudentContractsListQuery listQuery)
    {
        if (listQuery.AcademicYearId is { } academicYearId)
        {
            query = query.Where(sc => sc.AcademicYearId == academicYearId);
        }

        if (listQuery.StudentStatusId is { } studentStatusId)
        {
            query = query.Where(sc => sc.StudentStatusId == studentStatusId);
        }

        if (!string.IsNullOrEmpty(listQuery.Search))
        {
            string search = listQuery.Search;
            query = query.Where(sc => sc.ContractNumber.Contains(search) ||
                                      (sc.StudentHuman.LastName + " " + sc.StudentHuman.FirstName).Contains(search) ||
                                      (sc.StudentHuman.FirstName + " " + sc.StudentHuman.LastName).Contains(search) ||
                                      (sc.PayerHuman.LastName + " " + sc.PayerHuman.FirstName).Contains(search) ||
                                      (sc.PayerHuman.FirstName + " " + sc.PayerHuman.LastName).Contains(search));
        }

        return query;
    }

    //ბოლოს ScId-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<StudentContract> ApplySort(IQueryable<StudentContract> query,
        IReadOnlyList<StudentContractSortField> sortFields)
    {
        IOrderedQueryable<StudentContract>? ordered = null;
        foreach (StudentContractSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                EStudentContractSortField.ContractNumber => Order(query, ordered, sc => sc.ContractNumber,
                    sortField.Ascending),
                EStudentContractSortField.ContractDate => Order(query, ordered, sc => sc.ContractDate,
                    sortField.Ascending),
                EStudentContractSortField.StudentName => Order(query, ordered,
                    sc => sc.StudentHuman.LastName + " " + sc.StudentHuman.FirstName, sortField.Ascending),
                EStudentContractSortField.PayerName => Order(query, ordered,
                    sc => sc.PayerHuman.LastName + " " + sc.PayerHuman.FirstName, sortField.Ascending),
                EStudentContractSortField.AcademicYearName => Order(query, ordered,
                    sc => sc.AcademicYear.AcademicYearName, sortField.Ascending),
                EStudentContractSortField.StudentStatusName => Order(query, ordered,
                    sc => sc.StudentStatus == null ? null : sc.StudentStatus.StudentStatusName, sortField.Ascending),
                EStudentContractSortField.DesiredMonthlyPaymentDay => Order(query, ordered,
                    sc => sc.DesiredMonthlyPaymentDay, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, sc => sc.ScId, true);
    }

    private static IOrderedQueryable<StudentContract> Order<TKey>(IQueryable<StudentContract> query,
        IOrderedQueryable<StudentContract>? ordered, Expression<Func<StudentContract, TKey>> keySelector,
        bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
