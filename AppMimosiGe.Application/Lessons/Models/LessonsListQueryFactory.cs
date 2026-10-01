using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს გაკვეთილების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class LessonsListQueryFactory
{
    public const string GrpIdFilter = "grpId";

    //მასწავლებელი ან შემცვლელი
    public const string TeacherContractIdFilter = "teacherContractId";

    //"yyyy-MM-dd", ორივე ჩათვლით
    public const string DateFromFilter = "dateFrom";
    public const string DateToFilter = "dateTo";
    public const string LessonStatusIdFilter = "lessonStatusId";

    //"true": მხოლოდ შეუვსებელი გაკვეთილები
    public const string UnfilledFilter = "unfilled";

    public const int MaxRowsCount = 1000;

    private const string DateFormat = "yyyy-MM-dd";

    //ჟურნალი დროის მიხედვით იკითხება, ერთსა და იმავე დროს რამდენიმე ჯგუფისას კოდით
    public static readonly IReadOnlyList<LessonSortField> DefaultSortFields =
        [new(ELessonSortField.LessonDt, true), new(ELessonSortField.GroupCode, true)];

    public static Result<LessonsListQuery> Create(FilterSortRequest request, DateTime now)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return LessonErrors.FilterSortRequestIsInvalid;
        }

        int? grpId = null;
        int? teacherContractId = null;
        DateTime? dateFrom = null;
        DateTime? dateTo = null;
        int? lessonStatusId = null;
        bool unfilled = false;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = filter.FieldName switch
            {
                GrpIdFilter => TryParseId(value, out grpId),
                TeacherContractIdFilter => TryParseId(value, out teacherContractId),
                DateFromFilter => TryParseDate(value, out dateFrom),
                DateToFilter => TryParseDate(value, out dateTo),
                LessonStatusIdFilter => TryParseId(value, out lessonStatusId),
                UnfilledFilter => TryParseBool(value, out unfilled),
                _ => false
            };

            if (!isValid)
            {
                return LessonErrors.FilterSortRequestIsInvalid;
            }
        }

        if (dateFrom > dateTo)
        {
            return LessonErrors.FilterSortRequestIsInvalid;
        }

        List<LessonSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
            if (!Enum.TryParse(sortField.FieldName, true, out ELessonSortField field) || !Enum.IsDefined(field) ||
                int.TryParse(sortField.FieldName, out _))
            {
                return LessonErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new LessonSortField(field, sortField.Ascending));
        }

        return new LessonsListQuery(request.Offset, request.RowsCount, now, grpId, teacherContractId, dateFrom, dateTo,
            lessonStatusId, unfilled, sortFields.Count == 0 ? DefaultSortFields : sortFields);
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

    private static bool TryParseBool(string? value, out bool result)
    {
        result = false;
        return value is null || bool.TryParse(value, out result);
    }
}
