using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     გადათამაშების შედეგი (GroupOrdersReplay.Run): შეხებული ჯგუფები (D185) და მათი ახალი მდგომარეობები. შეხებული
///     ჯგუფი, რომელიც ცვლილების შემდეგ აღარ არსებობს (მისი შექმნის ბრძანება წაიშალა), States-ში არ არის. NotFitting:
///     პირველი ბრძანება, რომელიც მდგომარეობას აღარ ესადაგება; მაშინ States ცარიელია
/// </summary>
public sealed record GroupOrdersReplayResult(
    IReadOnlySet<int> AffectedGroupIds,
    IReadOnlyDictionary<int, GroupState> States,
    GroupOrderNotFitting? NotFitting);
