using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     კონტრაქტის სტრიქონი ჯგუფში (GroupsByStudents): ოთხკვირიანი გადასახადი, დასრულება და ჯგუფის გაუქმების თარიღი
/// </summary>
public sealed record DepositGroupStudentData(
    int StudentContractId,
    decimal FourWeekFee,
    DateTime? EndDate,
    DateTime? GroupVoidDate);
