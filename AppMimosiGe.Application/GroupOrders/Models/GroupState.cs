using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის მდგომარეობა, რომელიც მისი ბრძანებებიდან ითვლება (GroupOrdersFold): ველები, გაუქმების თარიღი და
///     მასწავლებლის, მოსწავლის და განრიგის სტრიქონები. ბაზის სტრიქონები მას ხაზის კავშირით ედრება (GROUP_ORDERS.md §5)
/// </summary>
public sealed record GroupState(
    GroupFields Fields,
    DateTime? VoidDate,
    IReadOnlyList<GroupTeacherRow> Teachers,
    IReadOnlyList<GroupStudentRow> Students,
    IReadOnlyList<GroupDayTimePlaceRow> DayTimePlaces);
