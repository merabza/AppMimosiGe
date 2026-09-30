using System.Collections.Generic;

namespace AppMimosiGe.Application.StudentContracts.Models;

/// <summary>
///     კონტრაქტების სიის ერთი გვერდის მოთხოვნა: ფილტრი, დალაგება და გვერდი.
///     Search ეძებს კონტრაქტის ნომერში და მოსწავლის ან გადამხდელის გვარ-სახელში
/// </summary>
public sealed record StudentContractsListQuery(
    int Offset,
    int RowsCount,
    int? AcademicYearId,
    int? StudentStatusId,
    string? Search,
    IReadOnlyList<StudentContractSortField> SortFields);
