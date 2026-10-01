using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Payments.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს გადახდების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class PaymentsListQueryFactory
{
    //Access-ის ფილტრი "მოსწავლე" კონტრაქტს ირჩევდა
    public const string StudentContractIdFilter = "studentContractId";
    public const string BankAccountIdFilter = "bankAccountId";

    //"yyyy-MM-dd", ორივე ჩათვლით
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    //Access-ის ფორმის დალაგება: თარიღი, მოსწავლე
    public static readonly IReadOnlyList<PaymentSortField> DefaultSortFields =
        [new(EPaymentSortField.PayDate, true), new(EPaymentSortField.StudentName, true)];

    public static Result<PaymentsListQuery> Create(FilterSortRequest request)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return PaymentErrors.FilterSortRequestIsInvalid;
        }

        int? studentContractId = null;
        int? bankAccountId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                StudentContractIdFilter => TryParseId(value, out studentContractId),
                BankAccountIdFilter => TryParseId(value, out bankAccountId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                _ => false
            };

            if (!isValid)
            {
                return PaymentErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return PaymentErrors.FilterSortRequestIsInvalid;
        }

        List<PaymentSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
            if (!Enum.TryParse(sortField.FieldName, true, out EPaymentSortField field) || !Enum.IsDefined(field) ||
                int.TryParse(sortField.FieldName, out _))
            {
                return PaymentErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new PaymentSortField(field, sortField.Ascending));
        }

        return new PaymentsListQuery(request.Offset, request.RowsCount, studentContractId, bankAccountId, dateFrom,
            dateTo, sortFields.Count == 0 ? DefaultSortFields : sortFields);
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
