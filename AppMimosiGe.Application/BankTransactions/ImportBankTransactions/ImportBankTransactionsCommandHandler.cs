using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.ImportBankTransactions;

/// <summary>
///     ბანკიდან ახალი ჩარიცხვების მიღება და ავტომატური განაწილება (D161, D164): ჩარიცხვა ინახება ბანკის
///     იდენტიფიკატორით (უკვე შენახული გამოტოვდება), ნაპოვნ მოსწავლეებზე კი მაშინვე იქმნება დაუდასტურებელი გადახდები
///     მიმდინარე წლის კონტრაქტზე. გადასვლის დღემდე (StartDate) არაფერი მოდის. ყველაფერი ერთი SaveChanges-ით ინახება;
///     ერთდროულად მხოლოდ ერთი მიღება მუშაობს
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ImportBankTransactionsCommandHandler(
    IBankMovementsClient bankClient,
    IBankTransactionsRepository repository,
    IBalancesRepository balancesRepository,
    IPaymentsRepository paymentsRepository,
    TimeProvider timeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<ImportBankTransactionsCommand, ImportBankTransactionsResponse>
{
    //ბოლო შენახული ჩარიცხვის წინა დღეებიც: ბანკი ჩარიცხვას შეიძლება გვიან აავტორიზებდეს
    public const int OverlapDays = 3;

    public const int DocumentMaxLength = 255;

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    public async Task<Result<ImportBankTransactionsResponse>> Handle(ImportBankTransactionsCommand command,
        CancellationToken cancellationToken)
    {
        IntegratedBankAccount? account = bankClient.Account;
        if (account is null)
        {
            return BankTransactionErrors.BankIsNotConfigured;
        }

        if (command.DateFrom > command.DateTo)
        {
            return BankTransactionErrors.PeriodIsInvalid;
        }

        if (!await ImportLock.WaitAsync(0, cancellationToken))
        {
            return BankTransactionErrors.ImportIsRunning;
        }

        try
        {
            return await Import(command, account, cancellationToken);
        }
        finally
        {
            ImportLock.Release();
        }
    }

    private async Task<Result<ImportBankTransactionsResponse>> Import(ImportBankTransactionsCommand command,
        IntegratedBankAccount account, CancellationToken cancellationToken)
    {
        DateTime today = timeProvider.GetLocalNow().Date;
        DateTime startDate = account.StartDate.Date;
        DateTime periodTo = (command.DateTo ?? today).Date;
        DateTime periodFrom = (command.DateFrom ??
                               (await repository.GetLastTransactionDate(account.BankAccountId, cancellationToken))
                               ?.AddDays(-OverlapDays) ?? startDate).Date;
        if (periodFrom < startDate)
        {
            periodFrom = startDate;
        }

        if (periodFrom > periodTo)
        {
            return new ImportBankTransactionsResponse(periodFrom, periodTo, 0, 0, 0, 0, 0, command.DryRun);
        }

        Result<IReadOnlyList<BankMovement>> received = await bankClient.GetIncomingPayments(
            DateOnly.FromDateTime(periodFrom), DateOnly.FromDateTime(periodTo), cancellationToken);
        if (received.IsFailure)
        {
            return Result.Failure<ImportBankTransactionsResponse>(received.Error);
        }

        IReadOnlyList<BankMovement> movements = received.Value;
        HashSet<string> existing = movements.Count == 0
            ? []
            : await repository.GetExistingMovementIds(account.BankAccountId,
                [.. movements.Select(m => m.MovementId)], cancellationToken);
        List<BankMovement> newMovements =
        [
            .. movements.Where(m => !existing.Contains(m.MovementId)).DistinctBy(m => m.MovementId)
                .OrderBy(m => m.Date).ThenBy(m => m.MovementId, StringComparer.Ordinal)
        ];

        List<PlannedTransaction> plan = newMovements.Count == 0
            ? []
            : BankTransactionsImportPlanner.Plan(newMovements, await LoadPlanInput(today, cancellationToken));

        if (!command.DryRun && plan.Count > 0)
        {
            await Save(plan, account, cancellationToken);
        }

        int allocated = plan.Count(p => p.Shares.Count > 0);
        return new ImportBankTransactionsResponse(periodFrom, periodTo, movements.Count,
            movements.Count - newMovements.Count, plan.Count, allocated, plan.Count - allocated, command.DryRun);
    }

    private async Task<ImportPlanInput> LoadPlanInput(DateTime today, CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        List<MatchContractData> contracts = CurrentAcademicYear.Find(academicYears, today) is { } academicYearId
            ? await repository.GetMatchContracts(academicYearId, cancellationToken)
            : [];
        PaymentMatchInput match = PaymentMatchInputFactory.Create(contracts,
            await repository.GetStudentByContractNumber(cancellationToken),
            await repository.GetLearnedPayers(cancellationToken));
        Dictionary<int, int> currentContractByStudent = PaymentMatchInputFactory.CurrentContractByStudent(contracts);

        //მოსწავლის ანგარიში: მიმდინარე კონტრაქტების მოსწავლეების ყველა წლის კონტრაქტი (ნაწილი 20)
        int[] currentScIds = [.. currentContractByStudent.Values];
        Dictionary<int, int> studentByContract = currentScIds.Length == 0
            ? []
            : await balancesRepository.GetStudentAccountContracts(currentScIds, cancellationToken);
        int[] scIds = [.. currentScIds.Union(studentByContract.Keys)];
        List<BalanceOperation> operations = scIds.Length == 0
            ? []
            : BalanceOperations.Build(await balancesRepository.GetCharges(scIds, cancellationToken),
                await balancesRepository.GetPayments(scIds, cancellationToken));

        return new ImportPlanInput(match, currentContractByStudent, operations, studentByContract);
    }

    private async Task Save(List<PlannedTransaction> plan, IntegratedBankAccount account,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;
        foreach (PlannedTransaction planned in plan)
        {
            BankMovement movement = planned.Movement;
            BankTransaction bankTransaction = new()
            {
                BankAccountId = account.BankAccountId,
                MovementId = movement.MovementId,
                TransactionDate = movement.Date,
                Amount = movement.Amount,
                Currency = movement.Currency,
                Purpose = Truncate(movement.Purpose, 500),
                AdditionalInformation = Truncate(movement.AdditionalInformation, 500),
                AdditionalDescription = Truncate(movement.AdditionalDescription, 500),
                DocumentNumber = Truncate(movement.DocumentNumber, 50),
                PartnerName = Truncate(movement.PartnerName, 255),
                PartnerAccountIban = Truncate(movement.PartnerIban, 50),
                PartnerBankCode = Truncate(movement.PartnerBankCode, 20),
                PartnerTaxCode = Truncate(movement.PartnerTaxCode, 20),
                PartnerPersonalNumber = Truncate(movement.PartnerPersonalNumber, 20),
                ImportedAt = now,
                MatchStep = (int)planned.Step
            };
            foreach (PlannedShare share in planned.Shares)
            {
                bankTransaction.Payments.Add(new Payment
                {
                    StudentContractId = share.StudentContractId,
                    PayDate = movement.Date.Date,
                    Amount = share.Amount,
                    Document = Truncate(movement.Purpose, DocumentMaxLength),
                    BankAccountId = account.BankAccountId,
                    Checked = false
                });
            }

            repository.Add(bankTransaction);
        }

        //გადახდა კონტრაქტის შემდეგი გადახდის თარიღს ცვლის (D78)
        await paymentsRepository.MarkNextPayDatesDirty(
            plan.SelectMany(p => p.Shares).Select(s => s.StudentContractId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        string? trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
