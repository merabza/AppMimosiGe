using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფების სტრიქონები, რომლებიც ბრძანების ხაზს უკავშირდება (GroupOrderTeacherId, GroupOrderStudentId,
///     GroupOrderDayTimePlaceId). ვიზარდი 2 მათ კავშირს შლის, ფაილში კი ხაზს მისი სტრიქონის Id ეწერება (D195)
/// </summary>
public sealed record GroupOrderLinkedRows(
    IReadOnlyList<GroupByTeacher> Teachers,
    IReadOnlyList<GroupByStudent> Students,
    IReadOnlyList<GroupDayTimePlace> DayTimePlaces);
