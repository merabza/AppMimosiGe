using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     კონტრაქტი ბალანსების სიისთვის: მოსწავლე და გადამხდელი "გვარი სახელი"-თ და ტელეფონებით, გადახდის სასურველი დღე და
///     შენახული შემდეგი გადახდის თარიღი
/// </summary>
public sealed record DepositContractData(
    int StudentContractId,
    int AcademicYearId,
    string StudentName,
    string ContractNumber,
    string? StudentPhone,
    string PayerName,
    string? PayerPhone,
    int? DesiredMonthlyPaymentDay,
    DateTime? NextPayDate);
