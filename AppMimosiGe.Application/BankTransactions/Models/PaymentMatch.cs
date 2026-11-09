using System.Collections.Generic;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ჩარიცხვის მოსწავლეები (ზრდადი ID-ებით) და წესი, რომლითაც მოიძებნნენ; None-ზე სია ცარიელია
/// </summary>
public sealed record PaymentMatch(EPaymentMatchStep Step, IReadOnlyList<int> StudentHumanIds);
