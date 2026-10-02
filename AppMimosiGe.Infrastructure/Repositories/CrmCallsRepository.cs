using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class CrmCallsRepository(IMimosiGeDbContext context) : ICrmCallsRepository
{
    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<CrmCallsRowsDataResponse> GetRowsData(CrmCallsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<CrmCall> filtered = ApplyFilter(context.CrmCalls.AsNoTracking(), query);

        int count = await filtered.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        IQueryable<CrmCallRowData> rows = filtered.Select(c => new CrmCallRowData
        {
            Id = c.CcId,
            StudentContractId = c.StudentContractId,
            //Access-ის ფორმის ჩამოსაშლელი სიის სახელი: გვარი სახელი / ნომერი
            StudentName =
                c.StudentContract.StudentHuman.LastName + " " + c.StudentContract.StudentHuman.FirstName + " / " +
                c.StudentContract.ContractNumber,
            CallDate = c.CallDate,
            CallTypeId = c.CallTypeId,
            CallTypeName = c.CallType.CallTypeName,
            AnswerTypeId = c.AnswerTypeId,
            AnswerTypeName = c.AnswerType.AnswerTypeName,
            CallConversation = c.CallConversation,
            MustPayDate = c.MustPayDate
        });

        List<CrmCallRowData> page = await ApplySort(rows, query.SortFields).Skip(offset).Take(query.RowsCount)
            .ToListAsync(cancellationToken);

        return new CrmCallsRowsDataResponse(count, offset, [
            .. page.Select(r => new CrmCallRowResponse(r.Id, r.StudentContractId, r.StudentName, r.CallDate,
                r.CallTypeId, r.CallTypeName, r.AnswerTypeId, r.AnswerTypeName, r.CallConversation, r.MustPayDate))
        ]);
    }

    public Task<CrmCallResponse?> GetOne(int crmCallId, CancellationToken cancellationToken = default)
    {
        return context.CrmCalls.AsNoTracking().Where(c => c.CcId == crmCallId).Select(c => new CrmCallResponse(c.CcId,
            c.StudentContractId,
            c.StudentContract.StudentHuman.LastName + " " + c.StudentContract.StudentHuman.FirstName + " / " +
            c.StudentContract.ContractNumber, c.StudentContract.AcademicYearId, c.CallTypeId, c.CallDate,
            c.AnswerTypeId, c.CallConversation, c.MustPayDate)).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<CrmCall?> GetForChange(int crmCallId, CancellationToken cancellationToken = default)
    {
        return context.CrmCalls.SingleOrDefaultAsync(c => c.CcId == crmCallId, cancellationToken);
    }

    public Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AnyAsync(x => x.ScId == scId, cancellationToken);
    }

    public Task<bool> CallTypeExists(int cctId, CancellationToken cancellationToken = default)
    {
        return context.CrmCallTypes.AnyAsync(x => x.CctId == cctId, cancellationToken);
    }

    public Task<bool> AnswerTypeExists(int catId, CancellationToken cancellationToken = default)
    {
        return context.CrmAnswerTypes.AnyAsync(x => x.CatId == catId, cancellationToken);
    }

    //სახელები უნიკალურია (UQ ინდექსი), ამიტომ სახელით დალაგება ცალსახაა
    public Task<List<LookupItemResponse>> GetCallTypes(CancellationToken cancellationToken = default)
    {
        return context.CrmCallTypes.AsNoTracking().OrderBy(x => x.CallTypeName)
            .Select(x => new LookupItemResponse(x.CctId, x.CallTypeName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetAnswerTypes(CancellationToken cancellationToken = default)
    {
        return context.CrmAnswerTypes.AsNoTracking().OrderBy(x => x.AnswerTypeName)
            .Select(x => new LookupItemResponse(x.CatId, x.AnswerTypeName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => x.AcademicYearId == academicYearId)
            .Select(x => new
            {
                x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).ThenBy(x => x.ScId).Select(x => new LookupItemResponse(x.ScId, x.Name))
            .ToListAsync(cancellationToken);
    }

    public void Add(CrmCall crmCall)
    {
        context.CrmCalls.Add(crmCall);
    }

    public void Remove(CrmCall crmCall)
    {
        context.CrmCalls.Remove(crmCall);
    }

    private static IQueryable<CrmCall> ApplyFilter(IQueryable<CrmCall> calls, CrmCallsListQuery query)
    {
        if (query.StudentContractId is { } studentContractId)
        {
            calls = calls.Where(c => c.StudentContractId == studentContractId);
        }

        if (query.DateFrom is { } dateFrom)
        {
            calls = calls.Where(c => c.CallDate >= dateFrom);
        }

        //DateTo ჩათვლით: ზარის თარიღს დროც აქვს, ამიტომ მომდევნო დღის დასაწყისამდე
        if (query.DateTo is { } dateTo)
        {
            DateTime dayAfter = dateTo.AddDays(1);
            calls = calls.Where(c => c.CallDate < dayAfter);
        }

        if (query.CallTypeId is { } callTypeId)
        {
            calls = calls.Where(c => c.CallTypeId == callTypeId);
        }

        if (query.AnswerTypeId is { } answerTypeId)
        {
            calls = calls.Where(c => c.AnswerTypeId == answerTypeId);
        }

        return calls;
    }

    //ბოლოს ID-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<CrmCallRowData> ApplySort(IQueryable<CrmCallRowData> query,
        IReadOnlyList<CrmCallSortField> sortFields)
    {
        IOrderedQueryable<CrmCallRowData>? ordered = null;
        foreach (CrmCallSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                ECrmCallSortField.CallDate => Order(query, ordered, r => r.CallDate, sortField.Ascending),
                ECrmCallSortField.StudentName => Order(query, ordered, r => r.StudentName, sortField.Ascending),
                ECrmCallSortField.CallTypeName => Order(query, ordered, r => r.CallTypeName, sortField.Ascending),
                ECrmCallSortField.AnswerTypeName => Order(query, ordered, r => r.AnswerTypeName, sortField.Ascending),
                ECrmCallSortField.MustPayDate => Order(query, ordered, r => r.MustPayDate, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.Id, true);
    }

    private static IOrderedQueryable<CrmCallRowData> Order<TKey>(IQueryable<CrmCallRowData> query,
        IOrderedQueryable<CrmCallRowData>? ordered, Expression<Func<CrmCallRowData, TKey>> keySelector, bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
