using System;
using System.Collections.Generic;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს თანამშრომლების კონტრაქტების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class TeacherContractsListQueryFactory
{
    //"true": მხოლოდ აქტიური კონტრაქტები, "false" ან ფილტრის არქონა: ყველა
    public const string ActiveOnlyFilter = "activeOnly";
    public const string SearchFilter = "search";

    public const int MaxRowsCount = 1000;

    //Access-ის ფორმის დალაგება: ნომერი, სახელი, სქემა
    public static readonly IReadOnlyList<TeacherContractSortField> DefaultSortFields =
    [
        new(ETeacherContractSortField.ContractNumber, true),
        new(ETeacherContractSortField.TeacherName, true),
        new(ETeacherContractSortField.SalarySchemeName, true)
    ];

    public static Result<TeacherContractsListQuery> Create(FilterSortRequest request, DateTime today)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return TeacherContractErrors.FilterSortRequestIsInvalid;
        }

        bool activeOnly = false;
        string? search = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            switch (filter.FieldName)
            {
                case ActiveOnlyFilter:
                    if (value is not null && !bool.TryParse(value, out activeOnly))
                    {
                        return TeacherContractErrors.FilterSortRequestIsInvalid;
                    }

                    break;
                case SearchFilter:
                    search = value;
                    break;
                default:
                    return TeacherContractErrors.FilterSortRequestIsInvalid;
            }
        }

        List<TeacherContractSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            if (!Enum.TryParse(sortField.FieldName, true, out ETeacherContractSortField field) ||
                !Enum.IsDefined(field) || int.TryParse(sortField.FieldName, out _))
            {
                return TeacherContractErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new TeacherContractSortField(field, sortField.Ascending));
        }

        return new TeacherContractsListQuery(request.Offset, request.RowsCount, activeOnly ? today.Date : null, search,
            sortFields.Count == 0 ? DefaultSortFields : sortFields);
    }
}
