using System;
using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ჯგუფის ბრძანება ძრავისთვის (GroupOrders). რომელი ნაწილი აქვს, ტიპზეა დამოკიდებული (GROUP_ORDERS.md §4): ჯგუფის
///     ველები მხოლოდ საწყის (0) და შექმნის (1) ბრძანებას აქვს, გაუქმების თარიღი მხოლოდ საწყისს
/// </summary>
public sealed record GroupOrderData
{
    /// <summary>
    ///     ბრძანების Id: ერთი დღის ბრძანებები Id-ის, ანუ შექმნის რიგით გაივლის (D185)
    /// </summary>
    public required int Id { get; init; }

    public required EGroupOrderType OrderType { get; init; }

    /// <summary>
    ///     ბრძანების თარიღი (a): ცვლილება ამ დღიდან მოქმედებს
    /// </summary>
    public required DateTime OrderDate { get; init; }

    /// <summary>
    ///     დაპაუზებული შექმნის ბრძანება: მასწავლებელი ან განრიგი აკლია, ამიტომ ჯგუფს ჯერ არ ქმნის (D186)
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>
    ///     ჯგუფი (გადაყვანისას საიდან)
    /// </summary>
    public int? GroupId { get; init; }

    /// <summary>
    ///     გადაყვანის სამიზნე ჯგუფი
    /// </summary>
    public int? TargetGroupId { get; init; }

    /// <summary>
    ///     ჯგუფის ველები (0 და 1)
    /// </summary>
    public GroupFields? Fields { get; init; }

    /// <summary>
    ///     ჯგუფის გაუქმების თარიღი (მხოლოდ 0)
    /// </summary>
    public DateTime? VoidDate { get; init; }

    public IReadOnlyList<GroupOrderTeacherLine> Teachers { get; init; } = [];
    public IReadOnlyList<GroupOrderStudentLine> Students { get; init; } = [];
    public IReadOnlyList<GroupOrderDayTimePlaceLine> DayTimePlaces { get; init; } = [];
}
