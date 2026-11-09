using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransaction;

public sealed record GetBankTransactionQuery(int BankTransactionId) : IQuery<BankTransactionResponse>;
