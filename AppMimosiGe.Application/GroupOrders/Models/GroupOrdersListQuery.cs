using System;
using System.Collections.Generic;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების სიის ერთი გვერდის მოთხოვნა: ფილტრი, დალაგება და გვერდი. GroupId პოულობს ჯგუფის ბრძანებებს, მათ შორის
///     გადაყვანას, რომლის სამიზნეც ეს ჯგუფია. DateFrom და DateTo ბრძანების თარიღის (a) შუალედია, ორივე ჩათვლით (D216).
///     IsPaused: true მხოლოდ დაპაუზებულები, false მხოლოდ დაუპაუზებლები, null ყველა
/// </summary>
public sealed record GroupOrdersListQuery(
    int Offset,
    int RowsCount,
    int? AcademicYearId,
    EGroupOrderType? OrderType,
    int? GroupId,
    DateTime? DateFrom,
    DateTime? DateTo,
    bool? IsPaused,
    IReadOnlyList<GroupOrderSortField> SortFields);
