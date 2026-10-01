using System;
using System.Collections.Generic;
using System.Globalization;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     carcass-ის GridView-ის FilterSortRequest-ს ჯგუფების სიის ტიპიზებულ მოთხოვნად გარდაქმნის.
///     ფილტრის და დალაგების ველების სახელები camelCase-ითაა, როგორც ფრონტი აგზავნის
/// </summary>
public static class GroupsListQueryFactory
{
    //"group" (ნაგულისხმევი), "teacher" ან "student"
    public const string FindMethodFilter = "findMethod";
    public const string AcademicYearIdFilter = "academicYearId";

    //"active", "voided" ან ფილტრის არქონა: ყველა
    public const string StateFilter = "state";
    public const string CourseIdFilter = "courseId";
    public const string GroupSizeIdFilter = "groupSizeId";
    public const string StudentStatusIdFilter = "studentStatusId";
    public const string SearchFilter = "search";

    public const int MaxRowsCount = 1000;

    //Access-ის cmbFind-ის დალაგება: ჯგუფით ძებნისას კოდი, მასწავლებლით და მოსწავლით ძებნისას გვარ-სახელი
    private static readonly IReadOnlyList<GroupSortField> DefaultGroupSortFields =
        [new(EGroupSortField.GroupCode, true)];

    private static readonly IReadOnlyList<GroupSortField> DefaultTeacherSortFields =
    [
        new(EGroupSortField.TeacherName, true), new(EGroupSortField.GroupCode, true),
        new(EGroupSortField.StartDate, true)
    ];

    private static readonly IReadOnlyList<GroupSortField> DefaultStudentSortFields =
    [
        new(EGroupSortField.StudentName, true), new(EGroupSortField.GroupCode, true),
        new(EGroupSortField.StartDate, true)
    ];

    public static IReadOnlyList<GroupSortField> DefaultSortFields(EGroupFindMethod findMethod)
    {
        return findMethod switch
        {
            EGroupFindMethod.Teacher => DefaultTeacherSortFields,
            EGroupFindMethod.Student => DefaultStudentSortFields,
            _ => DefaultGroupSortFields
        };
    }

    public static Result<GroupsListQuery> Create(FilterSortRequest request, DateTime today)
    {
        if (request.Offset < 0 || request.RowsCount is < 1 or > MaxRowsCount)
        {
            return GroupErrors.FilterSortRequestIsInvalid;
        }

        var findMethod = EGroupFindMethod.Group;
        int? academicYearId = null;
        EGroupState? state = null;
        int? courseId = null;
        int? groupSizeId = null;
        int? studentStatusId = null;
        string? search = null;

        foreach (ColumnFilter filter in request.FilterFields ?? [])
        {
            string? value = string.IsNullOrWhiteSpace(filter.Value) ? null : filter.Value.Trim();
            bool isValid = true;
            switch (filter.FieldName)
            {
                case FindMethodFilter:
                    if (value is not null)
                    {
                        isValid = TryParseEnum(value, out findMethod);
                    }

                    break;
                case AcademicYearIdFilter:
                    isValid = TryParseId(value, out academicYearId);
                    break;
                case StateFilter:
                    if (value is not null)
                    {
                        isValid = TryParseEnum(value, out EGroupState parsedState);
                        state = parsedState;
                    }

                    break;
                case CourseIdFilter:
                    isValid = TryParseId(value, out courseId);
                    break;
                case GroupSizeIdFilter:
                    isValid = TryParseId(value, out groupSizeId);
                    break;
                case StudentStatusIdFilter:
                    isValid = TryParseId(value, out studentStatusId);
                    break;
                case SearchFilter:
                    search = value;
                    break;
                default:
                    isValid = false;
                    break;
            }

            if (!isValid)
            {
                return GroupErrors.FilterSortRequestIsInvalid;
            }
        }

        List<GroupSortField> sortFields = [];
        foreach (SortField sortField in request.SortByFields ?? [])
        {
            if (!TryParseEnum(sortField.FieldName, out EGroupSortField field) || !IsSortable(field, findMethod))
            {
                return GroupErrors.FilterSortRequestIsInvalid;
            }

            sortFields.Add(new GroupSortField(field, sortField.Ascending));
        }

        return new GroupsListQuery(request.Offset, request.RowsCount, today.Date, findMethod, academicYearId, state,
            courseId, groupSizeId, studentStatusId, search,
            sortFields.Count == 0 ? DefaultSortFields(findMethod) : sortFields);
    }

    //სვეტი, რომელიც ძებნის ამ რეჟიმში სიაში არ ჩანს, დალაგებისთვისაც არ გამოდგება
    private static bool IsSortable(EGroupSortField field, EGroupFindMethod findMethod)
    {
        return field switch
        {
            EGroupSortField.ActiveStudentsCount => findMethod == EGroupFindMethod.Group,
            EGroupSortField.TeacherName => findMethod != EGroupFindMethod.Student,
            EGroupSortField.StudentName => findMethod == EGroupFindMethod.Student,
            EGroupSortField.StartDate or EGroupSortField.EndDate => findMethod != EGroupFindMethod.Group,
            _ => true
        };
    }

    //სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
    private static bool TryParseEnum<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        return Enum.TryParse(value, true, out result) && Enum.IsDefined(result) && !int.TryParse(value, out _);
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
