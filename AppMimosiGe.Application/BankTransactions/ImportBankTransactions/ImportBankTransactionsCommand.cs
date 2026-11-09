using System;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.ImportBankTransactions;

//DateFrom ცარიელი: ბოლო შენახული ჩარიცხვის თარიღს − 3 დღე (ან გადასვლის დღე); DateTo ცარიელი: დღეს
public sealed record ImportBankTransactionsCommand(DateTime? DateFrom, DateTime? DateTo, bool DryRun)
    : ICommand<ImportBankTransactionsResponse>;
