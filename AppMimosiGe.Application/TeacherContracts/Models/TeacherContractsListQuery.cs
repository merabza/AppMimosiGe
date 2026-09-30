using System;
using System.Collections.Generic;

namespace AppMimosiGe.Application.TeacherContracts.Models;

/// <summary>
///     თანამშრომლების კონტრაქტების სიის დალაგების ველები
/// </summary>
public enum ETeacherContractSortField
{
    ContractNumber,
    ContractDate,
    TeacherName,
    SalarySchemeName,
    PensionScheme,
    IndEnt,
    FixedAmount,
    ContractEndDate
}

public sealed record TeacherContractSortField(ETeacherContractSortField Field, bool Ascending);

/// <summary>
///     თანამშრომლების კონტრაქტების სიის ერთი გვერდის მოთხოვნა: ფილტრი, დალაგება და გვერდი.
///     ActiveOn: თუ შევსებულია, მხოლოდ ის კონტრაქტები, რომლებიც ამ დღისთვის არ დასრულებულა (დასრულების თარიღი
///     ცარიელია ან ეს დღე ან მისი შემდეგია). Search ეძებს კონტრაქტის ნომერში და თანამშრომლის გვარ-სახელში
/// </summary>
public sealed record TeacherContractsListQuery(
    int Offset,
    int RowsCount,
    DateTime? ActiveOn,
    string? Search,
    IReadOnlyList<TeacherContractSortField> SortFields);
