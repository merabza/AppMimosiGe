using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     ბალანსების სიის მონაცემები: კონტრაქტები, მათი ოპერაციები (BalanceOperations.Build), შემდეგი გაკვეთილები
///     (კონტრაქტის ID → თარიღი), CRM ზარები "უნდა გადაიხადოს" თარიღით, ჯგუფების სტრიქონები და ბოლო სამუშაო თვე
/// </summary>
public sealed record DepositsInput(
    IReadOnlyList<DepositContractData> Contracts,
    IReadOnlyList<BalanceOperation> Operations,
    IReadOnlyDictionary<int, DateTime> NextLessonDates,
    IReadOnlyList<CrmMustPayDateData> CrmMustPayDates,
    IReadOnlyList<DepositGroupStudentData> GroupStudents,
    DateTime? LastOperationMonth);
