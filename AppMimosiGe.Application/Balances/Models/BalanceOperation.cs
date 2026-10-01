using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     კონტრაქტის ერთი ოპერაცია: დარიცხვა (უარყოფითი თანხა, Id = LessonsByStudents-ის ID, დოკუმენტი საგნის სახელი) ან
///     გადახდა (Id = გადახდის ID). ორი სახის Id შეიძლება დაემთხვეს
/// </summary>
public sealed record BalanceOperation(
    bool IsPayment,
    int Id,
    int StudentContractId,
    DateTime OperationDate,
    string? Document,
    decimal Amount);
