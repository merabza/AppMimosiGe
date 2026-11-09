using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransaction;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetBankTransactionQueryHandler(IBankTransactionsRepository repository)
    : IQueryHandler<GetBankTransactionQuery, BankTransactionResponse>
{
    public async Task<Result<BankTransactionResponse>> Handle(GetBankTransactionQuery request,
        CancellationToken cancellationToken)
    {
        BankTransactionResponse? bankTransaction = await repository.GetOne(request.BankTransactionId, cancellationToken);
        return bankTransaction is null ? BankTransactionErrors.BankTransactionNotFound : bankTransaction;
    }
}
