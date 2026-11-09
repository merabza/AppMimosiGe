using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class BankTransactionsRepository(IMimosiGeDbContext context) : IBankTransactionsRepository
{
    public Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<DateTime?> GetLastTransactionDate(int bankAccountId, CancellationToken cancellationToken = default)
    {
        return context.BankTransactions.AsNoTracking().Where(x => x.BankAccountId == bankAccountId)
            .MaxAsync(x => (DateTime?)x.TransactionDate, cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingMovementIds(int bankAccountId,
        IReadOnlyCollection<string> movementIds, CancellationToken cancellationToken = default)
    {
        List<string> existing = await context.BankTransactions.AsNoTracking()
            .Where(x => x.BankAccountId == bankAccountId && movementIds.Contains(x.MovementId))
            .Select(x => x.MovementId).ToListAsync(cancellationToken);
        return [.. existing];
    }

    public Task<List<MatchContractData>> GetMatchContracts(int academicYearId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => x.AcademicYearId == academicYearId)
            .OrderBy(x => x.ScId).Select(x => new MatchContractData(x.ScId, x.StudentHumanId,
                x.StudentHuman.FirstName, x.StudentHuman.LastName, x.PayerHumanId, x.PayerHuman.PersonalId,
                x.PayerHuman.FirstName, x.PayerHuman.LastName)).ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetStudentByContractNumber(
        CancellationToken cancellationToken = default)
    {
        var contracts = await context.StudentContracts.AsNoTracking()
            .Select(x => new { x.ContractNumber, x.StudentHumanId, x.ScId }).ToListAsync(cancellationToken);
        return contracts.GroupBy(x => x.ContractNumber.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.MaxBy(x => x.ScId)!.StudentHumanId, StringComparer.Ordinal);
    }

    public Task<List<LearnedPayer>> GetLearnedPayers(CancellationToken cancellationToken = default)
    {
        return context.StudentPayers.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new LearnedPayer(x.StudentHumanId, x.PersonalNumber, x.AccountIban))
            .ToListAsync(cancellationToken);
    }

    public Task<List<BankTransactionRowResponse>> GetRows(DateTime? dateFrom, DateTime? dateTo,
        EBankTransactionsFilter filter, CancellationToken cancellationToken = default)
    {
        IQueryable<BankTransaction> query = context.BankTransactions.AsNoTracking();
        if (dateFrom is not null)
        {
            query = query.Where(x => x.TransactionDate >= dateFrom);
        }

        if (dateTo is not null)
        {
            DateTime dayAfter = dateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.TransactionDate < dayAfter);
        }

        query = filter switch
        {
            EBankTransactionsFilter.Unallocated => query.Where(x =>
                !x.Ignored && x.Payments.Sum(p => p.Amount) < x.Amount),
            EBankTransactionsFilter.Ignored => query.Where(x => x.Ignored),
            _ => query
        };

        return query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id)
            .Select(x => new BankTransactionRowResponse(x.Id, x.TransactionDate, x.Amount, x.PartnerName, x.Purpose,
                x.MatchStep, x.AllocatedManually, x.Ignored, x.Payments.Sum(p => p.Amount),
                x.Payments.Any(p => p.Checked))).ToListAsync(cancellationToken);
    }

    public async Task<BankTransactionResponse?> GetOne(int bankTransactionId,
        CancellationToken cancellationToken = default)
    {
        var bankTransaction = await context.BankTransactions.AsNoTracking().Where(x => x.Id == bankTransactionId)
            .Select(x => new
            {
                x.Id,
                x.TransactionDate,
                x.Amount,
                x.Currency,
                x.Purpose,
                x.AdditionalInformation,
                x.AdditionalDescription,
                x.DocumentNumber,
                x.PartnerName,
                x.PartnerAccountIban,
                x.PartnerTaxCode,
                x.MatchStep,
                x.AllocatedManually,
                x.Ignored
            }).SingleOrDefaultAsync(cancellationToken);
        if (bankTransaction is null)
        {
            return null;
        }

        List<BankTransactionAllocationResponse> allocations = await context.Payments.AsNoTracking()
            .Where(p => p.BankTransactionId == bankTransactionId).OrderBy(p => p.Id)
            .Select(p => new BankTransactionAllocationResponse(p.Id, p.StudentContractId,
                p.StudentContract.StudentHuman.LastName + " " + p.StudentContract.StudentHuman.FirstName + " " +
                p.StudentContract.ContractNumber, p.StudentContract.AcademicYearId, p.Amount, p.Checked))
            .ToListAsync(cancellationToken);

        return new BankTransactionResponse(bankTransaction.Id, bankTransaction.TransactionDate,
            bankTransaction.Amount, bankTransaction.Currency, bankTransaction.Purpose,
            bankTransaction.AdditionalInformation, bankTransaction.AdditionalDescription,
            bankTransaction.DocumentNumber, bankTransaction.PartnerName, bankTransaction.PartnerAccountIban,
            bankTransaction.PartnerTaxCode, bankTransaction.MatchStep, bankTransaction.AllocatedManually,
            bankTransaction.Ignored, allocations);
    }

    public Task<BankTransaction?> GetForChange(int bankTransactionId, CancellationToken cancellationToken = default)
    {
        return context.BankTransactions.Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == bankTransactionId, cancellationToken);
    }

    public Task<Dictionary<int, int>> GetStudentHumanIds(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => scIds.Contains(x.ScId))
            .ToDictionaryAsync(x => x.ScId, x => x.StudentHumanId, cancellationToken);
    }

    public Task<List<StudentPayer>> GetStudentPayers(IReadOnlyCollection<int> studentHumanIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentPayers.AsNoTracking().Where(x => studentHumanIds.Contains(x.StudentHumanId))
            .ToListAsync(cancellationToken);
    }

    public void Add(BankTransaction bankTransaction)
    {
        context.BankTransactions.Add(bankTransaction);
    }

    public void AddStudentPayer(StudentPayer studentPayer)
    {
        context.StudentPayers.Add(studentPayer);
    }

    public void RemovePayment(Payment payment)
    {
        context.Payments.Remove(payment);
    }
}
