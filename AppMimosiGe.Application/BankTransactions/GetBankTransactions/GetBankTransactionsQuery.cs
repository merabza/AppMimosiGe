using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;

namespace AppMimosiGe.Application.BankTransactions.GetBankTransactions;

//Filter: ცარიელი = ყველა, "unallocated" = ბოლომდე გაუნაწილებელი, "ignored" = არ არის მოსწავლის გადახდა
public sealed record GetBankTransactionsQuery(DateTime? DateFrom, DateTime? DateTo, string? Filter)
    : IQuery<List<BankTransactionRowResponse>>;
