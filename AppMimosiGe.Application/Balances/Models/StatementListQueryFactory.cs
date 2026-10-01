using System;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს ამონაწერის ტიპიზებულ მოთხოვნად გარდაქმნის. ფილტრის ველების სახელები
///     camelCase-ითაა, როგორც ფრონტი აგზავნის. დალაგების ველი არ მიიღება: running total ამონაწერის რიგზეა დამოკიდებული
/// </summary>
public static class StatementListQueryFactory
{
    public const string StudentContractIdFilter = "studentContractId";

    //"yyyy-MM-dd", ორივე ჩათვლით
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    public static Result<StatementListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount || request.SortByFields is { Length: > 0 })
        {
            return BalanceErrors.FilterSortRequestIsInvalid;
        }

        int? studentContractId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                StudentContractIdFilter => TryParseId(value, out studentContractId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                _ => false
            };

            if (!isValid)
            {
                return BalanceErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return BalanceErrors.FilterSortRequestIsInvalid;
        }

        return new StatementListQuery(request.Offset, request.RowsCount, studentContractId, dateFrom, dateTo);
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
}
