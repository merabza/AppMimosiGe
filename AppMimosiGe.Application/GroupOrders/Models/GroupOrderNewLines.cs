using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     შესანახი ბრძანების ახალი ხაზები, რომლებიც ბაზაში ჯერ არ არის, ძრავის დროებითი Id-ით (სტრიქონის LineId). ახალ
///     სტრიქონს ასეთი ხაზი navigation-ით უკავშირდება, ხაზის Id-ს კი SaveChanges ჩაწერს. ხაზი, რომელიც აქ არ არის, ბაზაშია
///     და სტრიქონს მისი Id ეწერება
/// </summary>
public sealed record GroupOrderNewLines(
    IReadOnlyDictionary<int, GroupOrderTeacher> Teachers,
    IReadOnlyDictionary<int, GroupOrderStudent> Students,
    IReadOnlyDictionary<int, GroupOrderDayTimePlace> DayTimePlaces)
{
    //ახალი ხაზების გარეშე, მაგალითად ბრძანების წაშლისას
    public static GroupOrderNewLines None { get; } = new(new Dictionary<int, GroupOrderTeacher>(),
        new Dictionary<int, GroupOrderStudent>(), new Dictionary<int, GroupOrderDayTimePlace>());
}
