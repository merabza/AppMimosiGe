using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.CrmCalls.Models;

/// <summary>
///     CRM ზარების სიის ერთი გვერდის მოთხოვნა: ფილტრი (კონტრაქტი, ზარის თარიღის შუალედი, ტიპი, შედეგი), დალაგება და
///     გვერდი. DateFrom და DateTo დღეებია, ორივე ჩათვლით
/// </summary>
public sealed record CrmCallsListQuery(
    int Offset,
    int RowsCount,
    int? StudentContractId,
    DateTime? DateFrom,
    DateTime? DateTo,
    int? CallTypeId,
    int? AnswerTypeId,
    IReadOnlyList<CrmCallSortField> SortFields);
