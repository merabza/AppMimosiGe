using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მდგომარეობა მისი ბრძანებებიდან (D185, GROUP_ORDERS.md §5 ნაბიჯი 3): ბრძანებები თარიღისა და Id-ის (შექმნის)
///     რიგით გაივლის, საწყისი (0) ან შექმნის (1) ბრძანებიდან. დაპაუზებული ბრძანება ეფექტს არ იძლევა (D186). პირველივე
///     ბრძანებაზე, რომელიც მდგომარეობას აღარ ესადაგება, გატარება ჩერდება
/// </summary>
public static class GroupOrdersFold
{
    /// <summary>
    ///     orders: ერთი ჯგუფის ყველა ბრძანება, ნებისმიერი რიგით
    /// </summary>
    public static GroupOrdersFoldResult Run(IEnumerable<GroupOrderData> orders)
    {
        GroupState? state = null;
        foreach (GroupOrderData order in orders.Where(o => !o.IsPaused).OrderBy(o => o.OrderDate).ThenBy(o => o.Id))
        {
            state = GroupOrderEffects.Apply(state, order);
            if (state is null)
            {
                return new GroupOrdersFoldResult(null, order.Id);
            }
        }

        return new GroupOrdersFoldResult(state, null);
    }
}
