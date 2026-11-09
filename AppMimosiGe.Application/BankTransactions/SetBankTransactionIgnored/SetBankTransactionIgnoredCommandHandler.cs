using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.SetBankTransactionIgnored;

/// <summary>
///     „არ არის მოსწავლის გადახდა“ (მაგ. დაბრუნება, სხვა შემოსავალი): ასეთი ჩარიცხვა არ ნაწილდება და გაუნაწილებელი
///     ჩარიცხვების რეპორტში არ ჩანს. განაწილებული ჩარიცხვა ჯერ უნდა გაუქმდეს (409)
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class SetBankTransactionIgnoredCommandHandler(
    IBankTransactionsRepository repository,
    IUnitOfWork unitOfWork) : ICommandHandler<SetBankTransactionIgnoredCommand>
{
    public async Task<Result> Handle(SetBankTransactionIgnoredCommand command, CancellationToken cancellationToken)
    {
        BankTransaction? bankTransaction = await repository.GetForChange(command.BankTransactionId, cancellationToken);
        if (bankTransaction is null)
        {
            return Result.Failure(BankTransactionErrors.BankTransactionNotFound);
        }

        if (command.Ignored && bankTransaction.Payments.Count > 0)
        {
            return Result.Failure(BankTransactionErrors.BankTransactionHasPayments);
        }

        bankTransaction.Ignored = command.Ignored;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
