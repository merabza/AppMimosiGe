using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.BankTransactions.ImportBankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using AppMimosiGe.Application.Payments;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;

/// <summary>
///     ჩარიცხვის ხელით განაწილება (D164, D168, D169): ჩარიცხვის გადახდები წილებს ემთხვევა (იმავე კონტრაქტის გადახდის
///     თანხა იცვლება, ზედმეტი იშლება, ახალი ემატება). ჯამი ჩარიცხულზე მეტი ვერ იქნება, შემოწმებული გადახდის მქონე
///     ჩარიცხვა აღარ იცვლება. ჩარიცხვის გადამხდელი ებმება წილების მოსწავლეებს (StudentPayers), რომ მომდევნო
///     ჩარიცხვებში პროგრამამ თვითონ იცნოს
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SetBankTransactionAllocationCommandHandler(
    IBankTransactionsRepository repository,
    IPaymentsRepository paymentsRepository,
    IUnitOfWork unitOfWork) : ICommandHandler<SetBankTransactionAllocationCommand>
{
    public async Task<Result> Handle(SetBankTransactionAllocationCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        List<BankTransactionShareRequest> shares = command.Request!.Shares;

        BankTransaction? bankTransaction = await repository.GetForChange(command.BankTransactionId, cancellationToken);
        if (bankTransaction is null)
        {
            return Result.Failure(BankTransactionErrors.BankTransactionNotFound);
        }

        if (bankTransaction.Ignored && shares.Count > 0)
        {
            return Result.Failure(BankTransactionErrors.BankTransactionIsIgnored);
        }

        if (bankTransaction.Payments.Any(p => p.Checked))
        {
            return Result.Failure(BankTransactionErrors.BankTransactionHasCheckedPayments);
        }

        if (shares.Sum(s => s.Amount) > bankTransaction.Amount)
        {
            return Result.Failure(BankTransactionErrors.AllocationExceedsAmount);
        }

        List<int> changedContracts = [.. bankTransaction.Payments.Select(p => p.StudentContractId)];
        ApplyShares(bankTransaction, shares);
        bankTransaction.AllocatedManually = true;

        int[] newContracts = [.. shares.Select(s => s.StudentContractId)];
        if (newContracts.Length > 0)
        {
            Dictionary<int, int> studentByContract =
                await repository.GetStudentHumanIds(newContracts, cancellationToken);
            int[] students = [.. studentByContract.Values.Distinct()];
            List<StudentPayer> existing = await repository.GetStudentPayers(students, cancellationToken);
            foreach (StudentPayer studentPayer in StudentPayerLearning.NewLinks(bankTransaction, students, existing))
            {
                repository.AddStudentPayer(studentPayer);
            }
        }

        //გადახდა კონტრაქტის შემდეგი გადახდის თარიღს ცვლის: ძველი და ახალი კონტრაქტები (D78)
        await paymentsRepository.MarkNextPayDatesDirty(changedContracts.Concat(newContracts), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private void ApplyShares(BankTransaction bankTransaction, List<BankTransactionShareRequest> shares)
    {
        foreach (Payment payment in bankTransaction.Payments.ToList())
        {
            BankTransactionShareRequest? share = shares.Find(s => s.StudentContractId == payment.StudentContractId);
            if (share is null)
            {
                bankTransaction.Payments.Remove(payment);
                repository.RemovePayment(payment);
            }
            else
            {
                payment.Amount = share.Amount;
            }
        }

        foreach (BankTransactionShareRequest share in shares.Where(s =>
                     bankTransaction.Payments.All(p => p.StudentContractId != s.StudentContractId)))
        {
            bankTransaction.Payments.Add(new Payment
            {
                StudentContractId = share.StudentContractId,
                PayDate = bankTransaction.TransactionDate.Date,
                Amount = share.Amount,
                Document = bankTransaction.Purpose is { Length: > ImportBankTransactionsCommandHandler.DocumentMaxLength }
                    ? bankTransaction.Purpose[..ImportBankTransactionsCommandHandler.DocumentMaxLength]
                    : bankTransaction.Purpose,
                BankAccountId = bankTransaction.BankAccountId,
                Checked = false
            });
        }
    }
}
