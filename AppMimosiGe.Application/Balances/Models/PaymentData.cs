using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     გადახდა (Access-ის vFrmChargesAndPayments-ის მეორე ნაწილი)
/// </summary>
public sealed record PaymentData(int Id, int StudentContractId, DateTime PayDate, string? Document, decimal Amount);
