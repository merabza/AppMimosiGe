using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     კონტრაქტი ბალანსების სიისთვის: მოსწავლე და გადამხდელი "გვარი სახელი"-თ და ტელეფონებით, გადახდის სასურველი დღე,
///     შენახული შემდეგი გადახდის თარიღი, მოსწავლის (Humans) ID და სასწავლო წლის დაწყება (მოსწავლის ბოლო კონტრაქტისთვის)
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
    DateTime? NextPayDate,
    int StudentHumanId,
    DateTime AcademicYearStartDate);
