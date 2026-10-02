using System;
using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.WorkHours;

public sealed class WorkHoursListQueryFactoryTests
{
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

    private static void AssertInvalid(Result<WorkHoursListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(WorkHourErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    // Access sorted the records by their start
    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllRecordsByStart()
    {
        Result<WorkHoursListQuery> result = WorkHoursListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        WorkHoursListQuery query = result.Value;
        Assert.Null(query.TeacherContractId);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Equal([new WorkHourSortField(EWorkHourSortField.WhStart, true)], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<WorkHoursListQuery> result = WorkHoursListQueryFactory.Create(Request([
            Filter("teacherContractId", " 15 "), Filter("dateFrom", "2026-09-01"), Filter("dateTo", "2026-09-30")
        ], offset: 20, rowsCount: 30));

        Assert.True(result.IsSuccess);
        WorkHoursListQuery query = result.Value;
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
        Assert.Equal(15, query.TeacherContractId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), query.DateFrom);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), query.DateTo);
    }

    // an empty value removes the filter, as the SPA sends it
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<WorkHoursListQuery> result = WorkHoursListQueryFactory.Create(Request([
            Filter("teacherContractId", ""), Filter("dateFrom", null), Filter("dateTo", " ")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.TeacherContractId);
        Assert.Null(result.Value.DateFrom);
        Assert.Null(result.Value.DateTo);
    }

    // one day is a valid range: both ends are included
    [Fact]
    public void Create_SameDayRange_IsValid()
    {
        Result<WorkHoursListQuery> result =
            WorkHoursListQueryFactory.Create(
                Request([Filter("dateFrom", "2026-09-24"), Filter("dateTo", "2026-09-24")]));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.DateFrom, result.Value.DateTo);
    }

    [Theory]
    [InlineData("teacherContractId", "x")]
    [InlineData("teacherContractId", "-1")]
    [InlineData("teacherContractId", "1.5")]
    [InlineData("dateFrom", "24.09.2026")]
    [InlineData("dateTo", "2026-13-01")]
    [InlineData("studentContractId", "1")]
    public void Create_InvalidFilter_IsInvalid(string fieldName, string value)
    {
        AssertInvalid(WorkHoursListQueryFactory.Create(Request([Filter(fieldName, value)])));
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsInvalid()
    {
        AssertInvalid(WorkHoursListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-25"), Filter("dateTo", "2026-09-24")
        ])));
    }

    [Fact]
    public void Create_ParsesSortFieldsIgnoringCase()
    {
        Result<WorkHoursListQuery> result = WorkHoursListQueryFactory.Create(Request(sorts:
            [Sort("employeeName", false), Sort("WHEND"), Sort("whStart", false)]));

        Assert.True(result.IsSuccess);
        Assert.Equal([
            new WorkHourSortField(EWorkHourSortField.EmployeeName, false),
            new WorkHourSortField(EWorkHourSortField.WhEnd, true),
            new WorkHourSortField(EWorkHourSortField.WhStart, false)
        ], result.Value.SortFields);
    }

    // the duration is computed after loading, so the server does not sort by it
    [Theory]
    [InlineData("hours")]
    [InlineData("0")]
    [InlineData("99")]
    public void Create_InvalidSortField_IsInvalid(string fieldName)
    {
        AssertInvalid(WorkHoursListQueryFactory.Create(Request(sorts: [Sort(fieldName)])));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_IsInvalid(int offset, int rowsCount)
    {
        AssertInvalid(WorkHoursListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1000)]
    public void Create_PageAtTheLimits_IsValid(int offset, int rowsCount)
    {
        Assert.True(WorkHoursListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)).IsSuccess);
    }
}
