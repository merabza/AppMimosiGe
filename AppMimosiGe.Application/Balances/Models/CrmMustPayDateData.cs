using System;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     CRM ზარი, რომელშიც "უნდა გადაიხადოს თარიღამდე" შევსებულია
/// </summary>
public sealed record CrmMustPayDateData(int CrmCallId, int StudentContractId, DateTime CallDate, DateTime MustPayDate);
