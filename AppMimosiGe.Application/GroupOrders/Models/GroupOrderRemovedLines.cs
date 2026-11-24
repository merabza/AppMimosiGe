using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანების ხაზები, რომლებიც შეცვლის მოთხოვნაში აღარ არის და უნდა წაიშალოს (GroupOrderEntityMapper.Write)
/// </summary>
public sealed record GroupOrderRemovedLines(
    List<GroupOrderTeacher> Teachers,
    List<GroupOrderStudent> Students,
    List<GroupOrderDayTimePlace> DayTimePlaces);
