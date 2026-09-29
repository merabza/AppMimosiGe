using System.Collections.Generic;

namespace AppMimosiGe.Application.StudentContracts.Models;

/// <summary>
///     კონტრაქტების სიის დალაგების ველები
/// </summary>
public enum EStudentContractSortField
{
    ContractNumber,
    ContractDate,
    StudentName,
    PayerName,
    AcademicYearName,
    StudentStatusName,
    DesiredMonthlyPaymentDay
}

public sealed record StudentContractSortField(EStudentContractSortField Field, bool Ascending);

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
