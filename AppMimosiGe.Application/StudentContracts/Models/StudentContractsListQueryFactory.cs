using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს კონტრაქტების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class StudentContractsListQueryFactory
{
    public const string AcademicYearIdFilter = "academicYearId";
    public const string StudentStatusIdFilter = "studentStatusId";
    public const string SearchFilter = "search";

    public const int MaxRowsCount = 1000;

    //Access-ის ფორმის დალაგება: ნომერი, მოსწავლე, მშობელი (გადამხდელი)
    public static readonly IReadOnlyList<StudentContractSortField> DefaultSortFields =
    [
        new(EStudentContractSortField.ContractNumber, true),
        new(EStudentContractSortField.StudentName, true),
        new(EStudentContractSortField.PayerName, true)
    ];

    public static Result<StudentContractsListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return StudentContractErrors.FilterSortRequestIsInvalid;
        }

        int? academicYearId = null;
        int? studentStatusId = null;
        string? search = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            switch (filter.FieldName)
            {
                case AcademicYearIdFilter:
                    if (!TryParseId(value, out academicYearId))
                    {
                        return StudentContractErrors.FilterSortRequestIsInvalid;
                    }

                    break;
                case StudentStatusIdFilter:
                    if (!TryParseId(value, out studentStatusId))
                    {
                        return StudentContractErrors.FilterSortRequestIsInvalid;
                    }

                    break;
                case SearchFilter:
                    search = value;
                    break;
                default:
                    return StudentContractErrors.FilterSortRequestIsInvalid;
            }
        }

        List<StudentContractSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            if (!Enum.TryParse(sortField.FieldName, true, out EStudentContractSortField field) ||
                !Enum.IsDefined(field) || int.TryParse(sortField.FieldName, out _))
            {
                return StudentContractErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new StudentContractSortField(field, sortField.Ascending));
        }

        return new StudentContractsListQuery(request.Offset, request.RowsCount, academicYearId, studentStatusId, search,
            sortFields.Count == 0 ? DefaultSortFields : sortFields);
    }

    //ცარიელი მნიშვნელობა ფილტრის მოხსნას ნიშნავს
    private static bool TryParseId(string? value, out int? id)
    {
        id = null;
        if (value is null)
        {
            return true;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            return false;
        }

        id = parsed;
        return true;
    }
}
