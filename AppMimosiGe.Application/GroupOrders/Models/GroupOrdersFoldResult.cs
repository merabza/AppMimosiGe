namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების გატარების შედეგი (GroupOrdersFold). NotFittingOrderId: ბრძანება, რომელიც ჯგუფის მდგომარეობას აღარ
///     ესადაგება (D185); მაშინ State null-ია. სხვა შემთხვევაში State ჯგუფის მდგომარეობაა, ან null, თუ ჯგუფი არ შექმნილა:
///     ბრძანება არ არის, ან შექმნის ბრძანება დაპაუზებულია (D186)
/// </summary>
public sealed record GroupOrdersFoldResult(GroupState? State, int? NotFittingOrderId);
