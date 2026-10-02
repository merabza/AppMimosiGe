using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს CRM ზარების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class CrmCallsListQueryFactory
{
    public const string StudentContractIdFilter = "studentContractId";

    //"yyyy-MM-dd", ორივე ჩათვლით
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";

    public const string CallTypeIdFilter = "callTypeId";
    public const string AnswerTypeIdFilter = "answerTypeId";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    //Access-ის FrmCRMCalls-ის დალაგება: თარიღი, "უნდა გადაიხადოს" კლებადობით, მოსწავლე
    public static readonly IReadOnlyList<CrmCallSortField> DefaultSortFields =
    [
        new(ECrmCallSortField.CallDate, true), new(ECrmCallSortField.MustPayDate, false),
        new(ECrmCallSortField.StudentName, true)
    ];

    public static Result<CrmCallsListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return CrmCallErrors.FilterSortRequestIsInvalid;
        }

        int? studentContractId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;
        int? callTypeId = null;
        int? answerTypeId = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                StudentContractIdFilter => TryParseId(value, out studentContractId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                CallTypeIdFilter => TryParseId(value, out callTypeId),
                AnswerTypeIdFilter => TryParseId(value, out answerTypeId),
                _ => false
            };

            if (!isValid)
            {
                return CrmCallErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return CrmCallErrors.FilterSortRequestIsInvalid;
        }

        List<CrmCallSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
            if (!Enum.TryParse(sortField.FieldName, true, out ECrmCallSortField field) || !Enum.IsDefined(field) ||
                int.TryParse(sortField.FieldName, out _))
            {
                return CrmCallErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new CrmCallSortField(field, sortField.Ascending));
        }

        return new CrmCallsListQuery(request.Offset, request.RowsCount, studentContractId, dateFrom, dateTo, callTypeId,
            answerTypeId, sortFields.Count == 0 ? DefaultSortFields : sortFields);
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
