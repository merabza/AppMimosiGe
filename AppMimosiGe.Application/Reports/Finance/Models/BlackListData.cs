using System.Collections.Generic;

namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     r15-ის მონაცემები: "უიმედო ვალის" გადახდები და მათი კონტრაქტების მოსწავლეები და გადამხდელები (ადამიანის ID-ით)
/// </summary>
public sealed record BlackListData(IReadOnlyList<DebtPayment> Payments, IReadOnlyDictionary<int, Debtor> Humans);
