using AppMimosiGeShared.Contracts.V1.Requests;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;

public sealed record SetBankTransactionAllocationCommand(
    int BankTransactionId,
    BankTransactionAllocationRequest? Request) : ICommand;
