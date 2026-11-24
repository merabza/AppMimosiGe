using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ვიზარდი 1-ის, „ბრძანებებზე გადასვლის", გეგმა (GroupOrdersSwitchPlanner, D194): დარღვევები და ყოველი ჯგუფი თავისი
///     საწყისი ბრძანებით ან მის გარეშე. თუ ერთი დარღვევაც არის, ვიზარდი არაფერს ცვლის. დარღვევები ჯგუფების სიას არ
///     ცვლის
/// </summary>
public sealed record GroupOrdersSwitchPlan(
    IReadOnlyList<GroupOrdersSwitchViolation> Violations,
    IReadOnlyList<GroupOrdersSwitchGroup> Groups);
