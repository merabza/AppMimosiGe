using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     უწყისის სტრიქონი
/// </summary>
public sealed record CalculatedSalaryLine(
    int TeacherContractId,
    decimal NetAmountRound,
    decimal AmountGross,
    decimal Pension2,
    decimal GrossMinusPension,
    decimal IncomeTax,
    decimal Gamokvitva,
    decimal Pension4,
    decimal AmountNet,
    DateTime MonthDate,
    int RsQuoteTypeId,
    decimal IndividualIncomeTax,
    IReadOnlyList<CalculatedSalaryLineDetail> Details);
