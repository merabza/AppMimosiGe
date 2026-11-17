using System.Collections.Generic;

namespace AppMimosiGe.Infrastructure.BankTransactions.TbcBis;

internal sealed record TbcMovementsResponse(List<TbcMovement>? Transactions, int Total);
