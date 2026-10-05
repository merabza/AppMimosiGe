using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.SharedKernel;
using Names = AppMimosiGeShared.Contracts.V1.Responses.ReportParameterNames;

namespace AppMimosiGe.Application.Reports;

/// <summary>
///     რეპორტის პარამეტრების შემოწმება, სათაურის მნიშვნელობები და Excel-ის ფაილის სახელი. query-ის ვალიდატორი არ
///     ეშვება (ValidationDecorator მხოლოდ ბრძანებებს ამოწმებს, D109), ამიტომ შემოწმება RunReportQueryHandler-შია
/// </summary>
public static class ReportParameterRules
{
    private const string DateFormat = "dd.MM.yyyy";
    private const string FileDateFormat = "yyyy-MM-dd";

    //არასავალდებულო ცარიელი ფილტრი (მასწავლებელი, საგანი, მოსწავლე, სასწავლო წელი)
    private const string AllValue = "ყველა";

    //თარიღები დღეებია: დრო იჭრება
    public static ReportParametersRequest Normalize(ReportParametersRequest parameters)
    {
        return parameters with { StartDate = parameters.StartDate?.Date, EndDate = parameters.EndDate?.Date };
    }

    //null: პარამეტრები სწორია. სავალდებულოს გარეშე → 400; "თარიღიდან" > "თარიღამდე" → 400
    public static Error? Validate(ReportDefinition definition, ReportParametersRequest parameters)
    {
        ReportParameter? missing =
            definition.Parameters.FirstOrDefault(p => p.Required && !HasValue(p.Name, parameters));
        if (missing is not null)
        {
            return ReportErrors.ParameterIsRequired(missing.Caption);
        }

        return Uses(definition, Names.StartDate) && Uses(definition, Names.EndDate) &&
               parameters.StartDate > parameters.EndDate
            ? ReportErrors.PeriodIsInvalid
            : null;
    }

    public static bool HasValue(string name, ReportParametersRequest parameters)
    {
        return name switch
        {
            Names.StartDate => parameters.StartDate.HasValue,
            Names.EndDate => parameters.EndDate.HasValue,
            Names.TeacherId => parameters.TeacherId.HasValue,
            Names.CourseId => parameters.CourseId.HasValue,
            Names.StudentId => parameters.StudentId.HasValue,
            Names.AcademicYearId => parameters.AcademicYearId.HasValue,
            _ => false
        };
    }

    //ჩამოსაშლელი სიიდან არჩეული პარამეტრის სახელს სათაურისთვის lookups სჭირდება
    public static bool NeedsLookups(ReportDefinition definition)
    {
        return definition.Parameters.Any(p =>
            p.Name is Names.TeacherId or Names.CourseId or Names.StudentId or Names.AcademicYearId);
    }

    //სათაურის მნიშვნელობა: თარიღი dd.MM.yyyy, მასწავლებელი, საგანი და მოსწავლე სახელით, ცარიელი ფილტრი "ყველა"
    public static string DisplayValue(string name, ReportParametersRequest parameters, ReportLookupsResponse? lookups)
    {
        return name switch
        {
            Names.StartDate => FormatDate(parameters.StartDate),
            Names.EndDate => FormatDate(parameters.EndDate),
            Names.TeacherId => LookupName(parameters.TeacherId, lookups?.Teachers),
            Names.CourseId => LookupName(parameters.CourseId, lookups?.Courses),
            Names.StudentId => LookupName(parameters.StudentId, lookups?.Students),
            Names.AcademicYearId => LookupName(parameters.AcademicYearId, lookups?.AcademicYears),
            _ => string.Empty
        };
    }

    //Excel-ის ფაილის სახელი: გასაღები და რეპორტის თარიღები, მაგ. r03RoomsAgenda_2026-10-02.xlsx (Access-ში
    //ReportName.xls). SPA იმავე წესით აწყობს სახელს, რადგან Content-Disposition სხვა origin-იდან არ ჩანს
    public static string ExcelFileName(ReportDefinition definition, ReportParametersRequest parameters)
    {
        IEnumerable<string> dates = definition.Parameters.Select(p => p.Name switch
        {
            Names.StartDate => parameters.StartDate,
            Names.EndDate => parameters.EndDate,
            _ => null
        }).OfType<DateTime>().Select(date => date.ToString(FileDateFormat, CultureInfo.InvariantCulture));
        return string.Join("_", [definition.Key, .. dates]) + ".xlsx";
    }

    private static bool Uses(ReportDefinition definition, string name)
    {
        return definition.Parameters.Any(p => p.Name == name);
    }

    private static string FormatDate(DateTime? date)
    {
        return date?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string LookupName(int? id, List<LookupItemResponse>? items)
    {
        if (id is null)
        {
            return AllValue;
        }

        return items?.FirstOrDefault(item => item.Id == id)?.Name ?? id.Value.ToString(CultureInfo.InvariantCulture);
    }
}
