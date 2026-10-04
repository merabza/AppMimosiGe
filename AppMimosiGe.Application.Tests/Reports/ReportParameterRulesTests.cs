using System;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportParameterRulesTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime End = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);

    //a report with a period and the three optional lookups, like r10 and r25 of part 18
    private static readonly ReportDefinition PeriodReport = ReportDefinition.Create("rTest", "Test", "Test report",
        [ReportsCatalog.ChecksCategoryKey], [
            new ReportParameter(ReportParameterNames.StartDate, ReportParameterCaptions.StartDate, true),
            new ReportParameter(ReportParameterNames.EndDate, ReportParameterCaptions.EndDate, true),
            new ReportParameter(ReportParameterNames.TeacherId, ReportParameterCaptions.Teacher, false),
            new ReportParameter(ReportParameterNames.CourseId, ReportParameterCaptions.Course, false),
            new ReportParameter(ReportParameterNames.StudentId, ReportParameterCaptions.Student, false)
        ], _ => new FakeReportQuery());

    //a report with the end date and a lookup, but without the start date
    private static readonly ReportDefinition EndDateAndTeacherReport = ReportDefinition.Create("rEnd", "End",
        "End date and teacher", [ReportsCatalog.ChecksCategoryKey], [
            new ReportParameter(ReportParameterNames.EndDate, ReportParameterCaptions.EndDate, true),
            new ReportParameter(ReportParameterNames.TeacherId, ReportParameterCaptions.Teacher, false)
        ], _ => new FakeReportQuery());

    private static readonly ReportDefinition NoParametersReport = ReportDefinition.Create("rNone", "None",
        "No parameters", [ReportsCatalog.ChecksCategoryKey], [], _ => new FakeReportQuery());

    private static readonly ReportLookupsResponse Lookups = new([new LookupItemResponse(5, "Alpha Ann / T5")],
        [new LookupItemResponse(3, "Math")], [new LookupItemResponse(20, "Beta Bob / 6.020")]);

    private static ReportParametersRequest Parameters(DateTime? start = null, DateTime? end = null,
        int? teacherId = null, int? courseId = null, int? studentId = null)
    {
        return new ReportParametersRequest(start, end, teacherId, courseId, studentId);
    }

    [Fact]
    public void Normalize_KeepsOnlyTheDays()
    {
        // Act
        ReportParametersRequest result = ReportParameterRules.Normalize(Parameters(Start.AddHours(13.5),
            End.AddSeconds(86399), 5, 3, 20));

        // Assert
        Assert.Equal(Parameters(Start, End, 5, 3, 20), result);
    }

    [Fact]
    public void Normalize_EmptyDates_StayEmpty()
    {
        Assert.Equal(Parameters(teacherId: 5), ReportParameterRules.Normalize(Parameters(teacherId: 5)));
    }

    [Fact]
    public void Validate_MissingRequiredParameter_IsAProblemWithItsCaption()
    {
        // Act
        Error? error = ReportParameterRules.Validate(ReportsCatalog.Find("r03RoomsAgenda")!, Parameters());

        // Assert
        Assert.NotNull(error);
        Assert.Equal(nameof(ReportErrors.ParameterIsRequired), error.Code);
        Assert.Equal(ErrorType.Problem, error.Type);
        Assert.Equal("„თარიღისთვის\" შევსებული უნდა იყოს", error.Description);
    }

    // the first missing required parameter is reported
    [Fact]
    public void Validate_PeriodWithoutDates_ReportsTheStartDate()
    {
        Assert.Equal("„თარიღიდან\" შევსებული უნდა იყოს",
            ReportParameterRules.Validate(PeriodReport, Parameters())?.Description);
    }

    [Fact]
    public void Validate_PeriodWithoutEndDate_ReportsTheEndDate()
    {
        Assert.Equal("„თარიღამდე\" შევსებული უნდა იყოს",
            ReportParameterRules.Validate(PeriodReport, Parameters(Start))?.Description);
    }

    [Fact]
    public void Validate_StartAfterEnd_IsAnInvalidPeriod()
    {
        Assert.Equal(ReportErrors.PeriodIsInvalid.Code,
            ReportParameterRules.Validate(PeriodReport, Parameters(End.AddDays(1), End))?.Code);
    }

    [Fact]
    public void Validate_OneDayPeriod_IsValid()
    {
        Assert.Null(ReportParameterRules.Validate(PeriodReport, Parameters(End, End)));
    }

    // optional lookups may stay empty
    [Fact]
    public void Validate_RequiredParametersPresent_IsValid()
    {
        Assert.Null(ReportParameterRules.Validate(ReportsCatalog.Find("r03RoomsAgenda")!, Parameters(end: End)));
    }

    // a report that has no start date does not compare the dates
    [Fact]
    public void Validate_ReportWithoutStartDate_IgnoresAStartAfterTheEnd()
    {
        Assert.Null(ReportParameterRules.Validate(ReportsCatalog.Find("r03RoomsAgenda")!,
            Parameters(End.AddDays(5), End)));
    }

    // another parameter does not make the report one with a period
    [Fact]
    public void Validate_ReportWithEndDateAndALookup_IgnoresAStartAfterTheEnd()
    {
        Assert.Null(ReportParameterRules.Validate(EndDateAndTeacherReport, Parameters(End.AddDays(5), End)));
    }

    [Theory]
    [InlineData(ReportParameterNames.StartDate)]
    [InlineData(ReportParameterNames.EndDate)]
    [InlineData(ReportParameterNames.TeacherId)]
    [InlineData(ReportParameterNames.CourseId)]
    [InlineData(ReportParameterNames.StudentId)]
    public void HasValue_EveryParameter(string name)
    {
        Assert.True(ReportParameterRules.HasValue(name, Parameters(Start, End, 5, 3, 20)));
        Assert.False(ReportParameterRules.HasValue(name, Parameters()));
    }

    [Fact]
    public void HasValue_UnknownParameter_IsFalse()
    {
        Assert.False(ReportParameterRules.HasValue("groupId", Parameters(Start, End, 5, 3, 20)));
    }

    [Fact]
    public void NeedsLookups_OnlyForTeacherCourseOrStudent()
    {
        Assert.True(ReportParameterRules.NeedsLookups(PeriodReport));
        Assert.False(ReportParameterRules.NeedsLookups(ReportsCatalog.Find("r03RoomsAgenda")!));
        Assert.False(ReportParameterRules.NeedsLookups(NoParametersReport));
    }

    [Theory]
    [InlineData(ReportParameterNames.TeacherId)]
    [InlineData(ReportParameterNames.CourseId)]
    [InlineData(ReportParameterNames.StudentId)]
    public void NeedsLookups_AnyOfTheLookups(string name)
    {
        var definition = ReportDefinition.Create("rOne", "One", "One lookup", [ReportsCatalog.ChecksCategoryKey],
            [new ReportParameter(name, "x", false)], _ => new FakeReportQuery());
        Assert.True(ReportParameterRules.NeedsLookups(definition));
    }

    [Fact]
    public void DisplayValue_DatesAreDayMonthYear()
    {
        Assert.Equal("01.09.2026",
            ReportParameterRules.DisplayValue(ReportParameterNames.StartDate, Parameters(Start), null));
        Assert.Equal("02.10.2026",
            ReportParameterRules.DisplayValue(ReportParameterNames.EndDate, Parameters(end: End), null));
        Assert.Equal(string.Empty, ReportParameterRules.DisplayValue(ReportParameterNames.EndDate, Parameters(), null));
    }

    [Fact]
    public void DisplayValue_LookupsShowTheNames()
    {
        ReportParametersRequest parameters = Parameters(teacherId: 5, courseId: 3, studentId: 20);
        Assert.Equal("Alpha Ann / T5",
            ReportParameterRules.DisplayValue(ReportParameterNames.TeacherId, parameters, Lookups));
        Assert.Equal("Math", ReportParameterRules.DisplayValue(ReportParameterNames.CourseId, parameters, Lookups));
        Assert.Equal("Beta Bob / 6.020",
            ReportParameterRules.DisplayValue(ReportParameterNames.StudentId, parameters, Lookups));
    }

    // an empty optional filter is "all"; an id that is not in the lists is shown as the id
    [Fact]
    public void DisplayValue_EmptyOrUnknownLookup()
    {
        Assert.Equal("ყველა", ReportParameterRules.DisplayValue(ReportParameterNames.TeacherId, Parameters(), Lookups));
        Assert.Equal("7",
            ReportParameterRules.DisplayValue(ReportParameterNames.CourseId, Parameters(courseId: 7), Lookups));
        Assert.Equal("20",
            ReportParameterRules.DisplayValue(ReportParameterNames.StudentId, Parameters(studentId: 20), null));
    }

    [Fact]
    public void DisplayValue_UnknownParameter_IsEmpty()
    {
        Assert.Equal(string.Empty, ReportParameterRules.DisplayValue("groupId", Parameters(Start, End), Lookups));
    }

    // the key and the report's dates, like Access's ReportName.xls
    [Fact]
    public void ExcelFileName_HasTheKeyAndTheReportsDates()
    {
        Assert.Equal("r03RoomsAgenda_2026-10-02.xlsx",
            ReportParameterRules.ExcelFileName(ReportsCatalog.Find("r03RoomsAgenda")!, Parameters(Start, End, 5)));
        Assert.Equal("rTest_2026-09-01_2026-10-02.xlsx",
            ReportParameterRules.ExcelFileName(PeriodReport, Parameters(Start, End, 5)));
        Assert.Equal("rNone.xlsx", ReportParameterRules.ExcelFileName(NoParametersReport, Parameters(Start, End)));
    }

    // a date that is not given is left out
    [Fact]
    public void ExcelFileName_MissingDate_IsLeftOut()
    {
        Assert.Equal("rTest_2026-10-02.xlsx", ReportParameterRules.ExcelFileName(PeriodReport, Parameters(end: End)));
    }

    private sealed record FakeReportQuery : IQuery<ReportTable>;
}
