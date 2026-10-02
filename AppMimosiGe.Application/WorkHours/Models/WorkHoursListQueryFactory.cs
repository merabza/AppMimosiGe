using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს ნამუშევარი დროის სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class WorkHoursListQueryFactory
{
    public const string TeacherContractIdFilter = "teacherContractId";

    //"yyyy-MM-dd", ორივე ჩათვლით
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    //Access-ის FrmWorkHours-ის დალაგება: სამუშაოს დაწყება
    public static readonly IReadOnlyList<WorkHourSortField> DefaultSortFields = [new(EWorkHourSortField.WhStart, true)];

    public static Result<WorkHoursListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return WorkHourErrors.FilterSortRequestIsInvalid;
        }

        int? teacherContractId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                TeacherContractIdFilter => TryParseId(value, out teacherContractId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                _ => false
            };

            if (!isValid)
            {
                return WorkHourErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return WorkHourErrors.FilterSortRequestIsInvalid;
        }

        List<WorkHourSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
            if (!Enum.TryParse(sortField.FieldName, true, out EWorkHourSortField field) || !Enum.IsDefined(field) ||
                int.TryParse(sortField.FieldName, out _))
            {
                return WorkHourErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new WorkHourSortField(field, sortField.Ascending));
        }

        return new WorkHoursListQuery(request.Offset, request.RowsCount, teacherContractId, dateFrom, dateTo,
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
