using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების გატარების შედეგი (GroupOrdersFold). States: ჯგუფების მდგომარეობები ჯგუფის Id-ით; ჯგუფი, რომელიც არ
///     შექმნილა (შექმნის ბრძანება არ არის ან დაპაუზებულია, D186), აქ არ არის. NotFitting: პირველი ბრძანება, რომელიც
///     მდგომარეობას აღარ ესადაგება (D185); მაშინ States ცარიელია
/// </summary>
public sealed record GroupOrdersFoldResult(
    IReadOnlyDictionary<int, GroupState> States,
    GroupOrderNotFitting? NotFitting);
