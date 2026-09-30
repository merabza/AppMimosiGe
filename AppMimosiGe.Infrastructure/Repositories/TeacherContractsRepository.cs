using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class TeacherContractsRepository(IMimosiGeDbContext context) : ITeacherContractsRepository
{
    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<TeacherContractsRowsDataResponse> GetRowsData(TeacherContractsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TeacherContract> filtered = ApplyFilter(context.TeacherContracts.AsNoTracking(), query);

        int count = await filtered.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        IOrderedQueryable<TeacherContract> sorted = ApplySort(filtered, query.SortFields);

        List<TeacherContractRowResponse> rows = await sorted.Skip(offset).Take(query.RowsCount).Select(tc =>
            new TeacherContractRowResponse(tc.Id, tc.ContractNumber, tc.ContractDate, tc.TeacherHumanId,
                tc.TeacherHuman.LastName + " " + tc.TeacherHuman.FirstName,
                tc.SalarySchemaByHours == null ? null : tc.SalarySchemaByHours.SchemaName, tc.PensionScheme, tc.IndEnt,
                tc.FixedAmount, tc.ContractEndDate)).ToListAsync(cancellationToken);

        return new TeacherContractsRowsDataResponse(count, offset, rows);
    }

    //დრო (1899-12-30 hh:mm) TimeOnly-ად გარდაიქმნება ჩატვირთვის შემდეგ
    public async Task<TeacherContractResponse?> GetOne(int id, CancellationToken cancellationToken = default)
    {
        var teacherContract = await context.TeacherContracts.AsNoTracking().Where(tc => tc.Id == id).Select(tc => new
        {
            Contract = tc, TeacherName = tc.TeacherHuman.LastName + " " + tc.TeacherHuman.FirstName
        }).SingleOrDefaultAsync(cancellationToken);
        if (teacherContract is null)
        {
            return null;
        }

        TeacherContract tc = teacherContract.Contract;
        return new TeacherContractResponse(tc.Id, tc.ContractNumber, tc.ContractDate, tc.TeacherHumanId,
            teacherContract.TeacherName, tc.BankAccount, tc.BankAccountCode, tc.PensionScheme, tc.IndEnt,
            tc.RsQuoteTypeId, tc.RsCountryId, tc.FixedAmount, tc.NextMonth, tc.Description, tc.SalarySchemaByHoursId,
            tc.WorkHourGroupId, ToTimeOnly(tc.WorkHoursStart), ToTimeOnly(tc.WorkHoursEnd), tc.ContractEndDate);
    }

    public Task<TeacherContract?> GetForChange(int id, CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.SingleOrDefaultAsync(tc => tc.Id == id, cancellationToken);
    }

    public Task<bool> ContractNumberExists(string contractNumber, int exceptId,
        CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AnyAsync(tc => tc.ContractNumber == contractNumber && tc.Id != exceptId,
            cancellationToken);
    }

    public async Task<bool> IsInUse(int id, CancellationToken cancellationToken = default)
    {
        return await context.GroupsByTeachers.AnyAsync(x => x.TeacherContractId == id, cancellationToken) ||
               await context.Lessons.AnyAsync(x => x.TeacherContractId == id || x.SubstituteTeacherContractId == id,
                   cancellationToken) ||
               await context.SalaryParts.AnyAsync(x => x.TeacherContractId == id, cancellationToken) ||
               await context.SalaryLines.AnyAsync(x => x.TeacherContractId == id, cancellationToken) ||
               await context.WorkHours.AnyAsync(x => x.TeacherContractId == id, cancellationToken);
    }

    public Task<bool> HumanExists(int humId, CancellationToken cancellationToken = default)
    {
        return context.Humans.AnyAsync(x => x.HumId == humId, cancellationToken);
    }

    public Task<bool> RsQuoteTypeExists(int qtId, CancellationToken cancellationToken = default)
    {
        return context.RsQuoteTypes.AnyAsync(x => x.QtId == qtId, cancellationToken);
    }

    public Task<bool> RsCountryExists(int id, CancellationToken cancellationToken = default)
    {
        return context.RsCountries.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> SalarySchemeExists(int id, CancellationToken cancellationToken = default)
    {
        return context.TeacherSalarySchemes.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> WorkHourGroupExists(int whgId, CancellationToken cancellationToken = default)
    {
        return context.WorkHourGroups.AnyAsync(x => x.WhgId == whgId, cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetRsQuoteTypes(CancellationToken cancellationToken = default)
    {
        return context.RsQuoteTypes.AsNoTracking().OrderBy(x => x.QtName)
            .Select(x => new LookupItemResponse(x.QtId, x.QtName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetRsCountries(CancellationToken cancellationToken = default)
    {
        return context.RsCountries.AsNoTracking().OrderBy(x => x.CountryName)
            .Select(x => new LookupItemResponse(x.Id, x.CountryName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetSalarySchemes(CancellationToken cancellationToken = default)
    {
        return context.TeacherSalarySchemes.AsNoTracking().OrderBy(x => x.SchemaName)
            .Select(x => new LookupItemResponse(x.Id, x.SchemaName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetWorkHourGroups(CancellationToken cancellationToken = default)
    {
        return context.WorkHourGroups.AsNoTracking().OrderBy(x => x.WhgKey)
            .Select(x => new LookupItemResponse(x.WhgId, x.WhgKey)).ToListAsync(cancellationToken);
    }

    public void Add(TeacherContract teacherContract)
    {
        context.TeacherContracts.Add(teacherContract);
    }

    public void Remove(TeacherContract teacherContract)
    {
        context.TeacherContracts.Remove(teacherContract);
    }

    private static TimeOnly? ToTimeOnly(DateTime? dateTime)
    {
        return dateTime is null ? null : TimeOnly.FromDateTime(dateTime.Value);
    }

    private static IQueryable<TeacherContract> ApplyFilter(IQueryable<TeacherContract> query,
        TeacherContractsListQuery listQuery)
    {
        if (listQuery.ActiveOn is { } activeOn)
        {
            query = query.Where(tc => tc.ContractEndDate == null || tc.ContractEndDate >= activeOn);
        }

        if (!string.IsNullOrEmpty(listQuery.Search))
        {
            string search = listQuery.Search;
            query = query.Where(tc => tc.ContractNumber.Contains(search) ||
                                      (tc.TeacherHuman.LastName + " " + tc.TeacherHuman.FirstName).Contains(search) ||
                                      (tc.TeacherHuman.FirstName + " " + tc.TeacherHuman.LastName).Contains(search));
        }

        return query;
    }

    //ბოლოს Id-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<TeacherContract> ApplySort(IQueryable<TeacherContract> query,
        IReadOnlyList<TeacherContractSortField> sortFields)
    {
        IOrderedQueryable<TeacherContract>? ordered = null;
        foreach (TeacherContractSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                ETeacherContractSortField.ContractNumber => Order(query, ordered, tc => tc.ContractNumber,
                    sortField.Ascending),
                ETeacherContractSortField.ContractDate => Order(query, ordered, tc => tc.ContractDate,
                    sortField.Ascending),
                ETeacherContractSortField.TeacherName => Order(query, ordered,
                    tc => tc.TeacherHuman.LastName + " " + tc.TeacherHuman.FirstName, sortField.Ascending),
                ETeacherContractSortField.SalarySchemeName => Order(query, ordered,
                    tc => tc.SalarySchemaByHours == null ? null : tc.SalarySchemaByHours.SchemaName,
                    sortField.Ascending),
                ETeacherContractSortField.PensionScheme => Order(query, ordered, tc => tc.PensionScheme,
                    sortField.Ascending),
                ETeacherContractSortField.IndEnt => Order(query, ordered, tc => tc.IndEnt, sortField.Ascending),
                ETeacherContractSortField.FixedAmount => Order(query, ordered, tc => tc.FixedAmount,
                    sortField.Ascending),
                ETeacherContractSortField.ContractEndDate => Order(query, ordered, tc => tc.ContractEndDate,
                    sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, tc => tc.Id, true);
    }

    private static IOrderedQueryable<TeacherContract> Order<TKey>(IQueryable<TeacherContract> query,
        IOrderedQueryable<TeacherContract>? ordered, Expression<Func<TeacherContract, TKey>> keySelector,
        bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
