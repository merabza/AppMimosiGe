using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class PaymentsRepository(IMimosiGeDbContext context) : IPaymentsRepository
{
    //ჯერ ფილტრი, დათვლა, ჯამი და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<PaymentsRowsDataResponse> GetRowsData(PaymentsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> filtered = ApplyFilter(context.Payments.AsNoTracking(), query);

        int count = await filtered.CountAsync(cancellationToken);

        //Access-ის ფორმის footer: =Sum([Amount]) ფილტრის ყველა ჩანაწერზე
        decimal totalAmount = await filtered.SumAsync(p => p.Amount, cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        IQueryable<PaymentRowData> rows = filtered.Select(p => new PaymentRowData
        {
            Id = p.Id,
            StudentContractId = p.StudentContractId,
            //Access-ის ფორმის ჩამოსაშლელი სიის სახელი: გვარი სახელი ნომერი
            StudentName = p.StudentContract.StudentHuman.LastName + " " + p.StudentContract.StudentHuman.FirstName +
                          " " + p.StudentContract.ContractNumber,
            PayDate = p.PayDate,
            Amount = p.Amount,
            Document = p.Document,
            BankAccountId = p.BankAccountId,
            BankName = p.BankAccount == null ? null : p.BankAccount.BankName,
            Checked = p.Checked
        });

        List<PaymentRowData> page = await ApplySort(rows, query.SortFields).Skip(offset).Take(query.RowsCount)
            .ToListAsync(cancellationToken);

        return new PaymentsRowsDataResponse(count, offset, totalAmount, [
            .. page.Select(r => new PaymentRowResponse(r.Id, r.StudentContractId, r.StudentName, r.PayDate, r.Amount,
                r.Document, r.BankAccountId, r.BankName, r.Checked))
        ]);
    }

    public Task<PaymentResponse?> GetOne(int paymentId, CancellationToken cancellationToken = default)
    {
        return context.Payments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => new PaymentResponse(p.Id,
            p.StudentContractId,
            p.StudentContract.StudentHuman.LastName + " " + p.StudentContract.StudentHuman.FirstName + " " +
            p.StudentContract.ContractNumber, p.StudentContract.AcademicYearId, p.PayDate, p.Amount, p.Document,
            p.BankAccountId, p.Checked)).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Payment?> GetForChange(int paymentId, CancellationToken cancellationToken = default)
    {
        return context.Payments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AnyAsync(x => x.ScId == scId, cancellationToken);
    }

    public Task<bool> BankAccountExists(int baId, CancellationToken cancellationToken = default)
    {
        return context.BankAccounts.AnyAsync(x => x.BaId == baId, cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetBankAccounts(CancellationToken cancellationToken = default)
    {
        return context.BankAccounts.AsNoTracking().OrderBy(x => x.BankName).ThenBy(x => x.BaId)
            .Select(x => new LookupItemResponse(x.BaId, x.BankName)).ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => x.AcademicYearId == academicYearId)
            .Select(x => new
            {
                x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " " + x.ContractNumber
            }).OrderBy(x => x.Name).ThenBy(x => x.ScId).Select(x => new LookupItemResponse(x.ScId, x.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x => scIds.Contains(x.ScId)).ToListAsync(cancellationToken);
    }

    public void Add(Payment payment)
    {
        context.Payments.Add(payment);
    }

    public void Remove(Payment payment)
    {
        context.Payments.Remove(payment);
    }

    private static IQueryable<Payment> ApplyFilter(IQueryable<Payment> payments, PaymentsListQuery query)
    {
        if (query.StudentContractId is { } studentContractId)
        {
            payments = payments.Where(p => p.StudentContractId == studentContractId);
        }

        if (query.BankAccountId is { } bankAccountId)
        {
            payments = payments.Where(p => p.BankAccountId == bankAccountId);
        }

        if (query.DateFrom is { } dateFrom)
        {
            payments = payments.Where(p => p.PayDate >= dateFrom);
        }

        //DateTo ჩათვლით: მისი ბოლომდე (Access-ის ნაგულისხმევი "თარიღამდე" დღის 23:59:59-ია)
        if (query.DateTo is { } dateTo)
        {
            DateTime dayAfter = dateTo.AddDays(1);
            payments = payments.Where(p => p.PayDate < dayAfter);
        }

        return payments;
    }

    //ბოლოს ID-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<PaymentRowData> ApplySort(IQueryable<PaymentRowData> query,
        IReadOnlyList<PaymentSortField> sortFields)
    {
        IOrderedQueryable<PaymentRowData>? ordered = null;
        foreach (PaymentSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                EPaymentSortField.PayDate => Order(query, ordered, r => r.PayDate, sortField.Ascending),
                EPaymentSortField.StudentName => Order(query, ordered, r => r.StudentName, sortField.Ascending),
                EPaymentSortField.Amount => Order(query, ordered, r => r.Amount, sortField.Ascending),
                EPaymentSortField.Document => Order(query, ordered, r => r.Document, sortField.Ascending),
                EPaymentSortField.BankName => Order(query, ordered, r => r.BankName, sortField.Ascending),
                EPaymentSortField.Checked => Order(query, ordered, r => r.Checked, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.Id, true);
    }

    private static IOrderedQueryable<PaymentRowData> Order<TKey>(IQueryable<PaymentRowData> query,
        IOrderedQueryable<PaymentRowData>? ordered, Expression<Func<PaymentRowData, TKey>> keySelector, bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
