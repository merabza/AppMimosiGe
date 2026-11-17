using System.Collections.Generic;

namespace AppMimosiGe.Infrastructure.BankTransactions.BogBonline;

internal sealed record BogStatementResponse(long Id, int Count, int TotalCount, List<BogStatementRecord>? Records);
