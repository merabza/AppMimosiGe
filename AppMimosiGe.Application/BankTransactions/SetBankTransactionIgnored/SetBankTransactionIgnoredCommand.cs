using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.SetBankTransactionIgnored;

public sealed record SetBankTransactionIgnoredCommand(int BankTransactionId, bool Ignored) : ICommand;
