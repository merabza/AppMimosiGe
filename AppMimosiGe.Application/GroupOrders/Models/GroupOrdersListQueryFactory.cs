using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს ბრძანებების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class GroupOrdersListQueryFactory
{
    public const string AcademicYearIdFilter = "academicYearId";

    //ბრძანების ტიპი რიცხვით, 0–10
    public const string OrderTypeFilter = "orderType";
    public const string GroupIdFilter = "groupId";

    //ბრძანების თარიღი (a), "yyyy-MM-dd", ორივე ჩათვლით (D216)
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";

    //"true" ან "false"; ფილტრის არქონა: ყველა
    public const string IsPausedFilter = "isPaused";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    //ახალი ბრძანებები პირველი (D217); საწყის ბრძანებებს ნომერი არ აქვს, ამიტომ ისინი ბოლოსაა, თარიღის კლებით
    public static readonly IReadOnlyList<GroupOrderSortField> DefaultSortFields =
        [new(EGroupOrderSortField.Number, false), new(EGroupOrderSortField.OrderDate, false)];

    public static Result<GroupOrdersListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return GroupOrderErrors.FilterSortRequestIsInvalid;
        }

        int? academicYearId = null;
        EGroupOrderType? orderType = null;
        int? groupId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;
        bool? isPaused = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                AcademicYearIdFilter => TryParseId(value, out academicYearId),
                OrderTypeFilter => TryParseOrderType(value, out orderType),
                GroupIdFilter => TryParseId(value, out groupId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                IsPausedFilter => TryParseBool(value, out isPaused),
                _ => false
            };

            if (!isValid)
            {
                return GroupOrderErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return GroupOrderErrors.FilterSortRequestIsInvalid;
        }

        List<GroupOrderSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
            if (!Enum.TryParse(sortField.FieldName, true, out EGroupOrderSortField field) || !Enum.IsDefined(field) ||
                int.TryParse(sortField.FieldName, out _))
            {
                return GroupOrderErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new GroupOrderSortField(field, sortField.Ascending));
        }

        return new GroupOrdersListQuery(request.Offset, request.RowsCount, academicYearId, orderType, groupId,
            dateFrom, dateTo, isPaused, sortFields.Count == 0 ? DefaultSortFields : sortFields);
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

    private static bool TryParseOrderType(string? value, out EGroupOrderType? orderType)
    {
        orderType = null;
        if (!TryParseId(value, out int? id))
        {
            return false;
        }

        if (id is not { } typeId)
        {
            return true;
        }

        if (!Enum.IsDefined((EGroupOrderType)typeId))
        {
            return false;
        }

        orderType = (EGroupOrderType)typeId;
        return true;
    }

    private static bool TryParseDate(string? value, out DateTime? date)
    {
        date = null;
        if (value is null)
        {
            return true;
        }

        if (!DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateTime parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }

    private static bool TryParseBool(string? value, out bool? flag)
    {
        flag = null;
        if (value is null)
        {
            return true;
        }

        if (!bool.TryParse(value, out bool parsed))
        {
            return false;
        }

        flag = parsed;
        return true;
    }
}
