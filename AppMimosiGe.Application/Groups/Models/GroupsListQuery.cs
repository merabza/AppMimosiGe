using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფების სიის ერთი გვერდის მოთხოვნა: ძებნის რეჟიმი, ფილტრი, დალაგება და გვერდი.
///     Today: დღევანდელი თარიღი მდგომარეობის ფილტრისთვის, მიმდინარე მასწავლებლისა და აქტიური მოსწავლეების დასათვლელად.
///     Search ეძებს ჯგუფის კოდში (ჯგუფით), მასწავლებლის (მასწავლებლით) ან მოსწავლის (მოსწავლით) გვარ-სახელში
/// </summary>
public sealed record GroupsListQuery(
    int Offset,
    int RowsCount,
    DateTime Today,
    EGroupFindMethod FindMethod,
    int? AcademicYearId,
    EGroupState? State,
    int? CourseId,
    int? GroupSizeId,
    int? StudentStatusId,
    string? Search,
    IReadOnlyList<GroupSortField> SortFields);
