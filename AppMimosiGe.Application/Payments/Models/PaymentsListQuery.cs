using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Payments.Models;

/// <summary>
///     გადახდების სიის ერთი გვერდის მოთხოვნა: ფილტრი (Access-ის FrmPayments-ის ოთხი ფილტრი), დალაგება და გვერდი.
///     DateFrom და DateTo დღეებია, ორივე ჩათვლით
/// </summary>
public sealed record PaymentsListQuery(
    int Offset,
    int RowsCount,
    int? StudentContractId,
    int? BankAccountId,
    DateTime? DateFrom,
    DateTime? DateTo,
    IReadOnlyList<PaymentSortField> SortFields);
