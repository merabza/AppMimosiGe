using System;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Lessons;

public sealed class LessonsListQueryFactoryTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 13, 30, 0, DateTimeKind.Unspecified);

    private static FilterSortRequest Request(ColumnFilter[]? filters = null, SortField[]? sorts = null, int offset = 0,
        int rowsCount = 10)
    {
        return new FilterSortRequest(offset, rowsCount, filters, sorts);
    }

    private static ColumnFilter Filter(string fieldName, string? value)
    {
        return new ColumnFilter { FieldName = fieldName, Value = value };
    }

    private static SortField Sort(string fieldName, bool ascending = true)
    {
        return new SortField(ascending, fieldName);
    }

    private static void AssertInvalid(Result<LessonsListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(LessonErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllLessonsByTimeAndGroup()
    {
        Result<LessonsListQuery> result = LessonsListQueryFactory.Create(Request(), Now);

        Assert.True(result.IsSuccess);
        LessonsListQuery query = result.Value;
        Assert.Equal(Now, query.Now);
        Assert.Null(query.GrpId);
        Assert.Null(query.TeacherContractId);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Null(query.LessonStatusId);
        Assert.False(query.Unfilled);
        Assert.Equal(
        [
            new LessonSortField(ELessonSortField.LessonDt, true), new LessonSortField(ELessonSortField.GroupCode, true)
        ], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<LessonsListQuery> result = LessonsListQueryFactory.Create(Request([
            Filter("grpId", "7"), Filter("teacherContractId", " 5 "), Filter("dateFrom", "2026-09-28"),
            Filter("dateTo", "2026-10-04"), Filter("lessonStatusId", "2"), Filter("unfilled", "true")
        ], offset: 20, rowsCount: 30), Now);

        Assert.True(result.IsSuccess);
        LessonsListQuery query = result.Value;
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
        Assert.Equal(7, query.GrpId);
        Assert.Equal(5, query.TeacherContractId);
        Assert.Equal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Unspecified), query.DateFrom);
        Assert.Equal(new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Unspecified), query.DateTo);
        Assert.Equal(2, query.LessonStatusId);
        Assert.True(query.Unfilled);
    }

    // an empty value removes the filter, as the SPA sends it
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<LessonsListQuery> result = LessonsListQueryFactory.Create(Request([
            Filter("grpId", ""), Filter("teacherContractId", " "), Filter("dateFrom", null), Filter("dateTo", ""),
            Filter("lessonStatusId", null), Filter("unfilled", "")
        ]), Now);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.GrpId);
        Assert.Null(result.Value.TeacherContractId);
        Assert.Null(result.Value.DateFrom);
        Assert.Null(result.Value.DateTo);
        Assert.Null(result.Value.LessonStatusId);
        Assert.False(result.Value.Unfilled);
    }

    [Fact]
    public void Create_UnfilledFalse_IsNoFilter()
    {
        Assert.False(LessonsListQueryFactory.Create(Request([Filter("unfilled", "false")]), Now).Value.Unfilled);
    }

    // one day is a valid range: both ends are included
    [Fact]
    public void Create_SameDayRange_IsValid()
    {
        Result<LessonsListQuery> result = LessonsListQueryFactory.Create(
            Request([Filter("dateFrom", "2026-10-01"), Filter("dateTo", "2026-10-01")]), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.DateFrom, result.Value.DateTo);
    }

    [Theory]
    [InlineData("grpId", "x")]
    [InlineData("grpId", "-1")]
    [InlineData("teacherContractId", "1.5")]
    [InlineData("lessonStatusId", "two")]
    [InlineData("dateFrom", "01.10.2026")]
    [InlineData("dateTo", "2026-13-01")]
    [InlineData("unfilled", "yes")]
    [InlineData("search", "a")]
    public void Create_InvalidFilter_IsInvalid(string fieldName, string value)
    {
        AssertInvalid(LessonsListQueryFactory.Create(Request([Filter(fieldName, value)]), Now));
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsInvalid()
    {
        AssertInvalid(LessonsListQueryFactory.Create(
            Request([Filter("dateFrom", "2026-10-02"), Filter("dateTo", "2026-10-01")]), Now));
    }

    [Fact]
    public void Create_ParsesSortFieldsIgnoringCase()
    {
        Result<LessonsListQuery> result = LessonsListQueryFactory.Create(Request(sorts:
        [
            Sort("presentCount", false), Sort("substituteTeacherName"), Sort("LESSONSTATUSNAME", false)
        ]), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal([
            new LessonSortField(ELessonSortField.PresentCount, false),
            new LessonSortField(ELessonSortField.SubstituteTeacherName, true),
            new LessonSortField(ELessonSortField.LessonStatusName, false)
        ], result.Value.SortFields);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    [InlineData("99")]
    public void Create_InvalidSortField_IsInvalid(string fieldName)
    {
        AssertInvalid(LessonsListQueryFactory.Create(Request(sorts: [Sort(fieldName)]), Now));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_IsInvalid(int offset, int rowsCount)
    {
        AssertInvalid(LessonsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount), Now));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1000)]
    public void Create_PageAtTheLimits_IsValid(int offset, int rowsCount)
    {
        Assert.True(LessonsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount), Now).IsSuccess);
    }
}
