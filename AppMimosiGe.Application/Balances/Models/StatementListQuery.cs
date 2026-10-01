using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     ამონაწერის ერთი გვერდის მოთხოვნა: ფილტრი (Access-ის FrmChargesAndPayments: კონტრაქტი, თარიღიდან, თარიღამდე) და
///     გვერდი. DateFrom და DateTo დღეებია, ორივე ჩათვლით. ამონაწერს მხოლოდ თავისი რიგი აქვს, ამიტომ დალაგება არ არის
/// </summary>
public sealed record StatementListQuery(
    int Offset,
    int RowsCount,
    int? StudentContractId,
    DateTime? DateFrom,
    DateTime? DateTo);
