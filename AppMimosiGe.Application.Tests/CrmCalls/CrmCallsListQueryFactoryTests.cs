using System;
using AppMimosiGe.Application.CrmCalls.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.CrmCalls;

public sealed class CrmCallsListQueryFactoryTests
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

    private static void AssertInvalid(Result<CrmCallsListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(CrmCallErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    // Access sorted the calls by date, "must pay by" descending and student
    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllCallsInTheAccessOrder()
    {
        Result<CrmCallsListQuery> result = CrmCallsListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        CrmCallsListQuery query = result.Value;
        Assert.Null(query.StudentContractId);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Null(query.CallTypeId);
        Assert.Null(query.AnswerTypeId);
        Assert.Equal([
            new CrmCallSortField(ECrmCallSortField.CallDate, true),
            new CrmCallSortField(ECrmCallSortField.MustPayDate, false),
            new CrmCallSortField(ECrmCallSortField.StudentName, true)
        ], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<CrmCallsListQuery> result = CrmCallsListQueryFactory.Create(Request([
            Filter("studentContractId", "12"), Filter("dateFrom", "2026-09-01"), Filter("dateTo", "2026-09-30"),
            Filter("callTypeId", " 1 "), Filter("answerTypeId", "3")
        ], offset: 20, rowsCount: 30));

        Assert.True(result.IsSuccess);
        CrmCallsListQuery query = result.Value;
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
        Assert.Equal(12, query.StudentContractId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), query.DateFrom);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), query.DateTo);
        Assert.Equal(1, query.CallTypeId);
        Assert.Equal(3, query.AnswerTypeId);
    }

    // an empty value removes the filter, as the SPA sends it
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<CrmCallsListQuery> result = CrmCallsListQueryFactory.Create(Request([
            Filter("studentContractId", ""), Filter("dateFrom", null), Filter("dateTo", " "),
            Filter("callTypeId", ""), Filter("answerTypeId", null)
        ]));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.StudentContractId);
        Assert.Null(result.Value.DateFrom);
        Assert.Null(result.Value.DateTo);
        Assert.Null(result.Value.CallTypeId);
        Assert.Null(result.Value.AnswerTypeId);
    }

    // one day is a valid range: both ends are included
    [Fact]
    public void Create_SameDayRange_IsValid()
    {
        Result<CrmCallsListQuery> result = CrmCallsListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-24"), Filter("dateTo", "2026-09-24")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.DateFrom, result.Value.DateTo);
    }

    [Theory]
    [InlineData("studentContractId", "x")]
    [InlineData("studentContractId", "-1")]
    [InlineData("callTypeId", "1.5")]
    [InlineData("answerTypeId", "a")]
    [InlineData("dateFrom", "24.09.2026")]
    [InlineData("dateTo", "2026-13-01")]
    [InlineData("bankAccountId", "1")]
    public void Create_InvalidFilter_IsInvalid(string fieldName, string value)
    {
        AssertInvalid(CrmCallsListQueryFactory.Create(Request([Filter(fieldName, value)])));
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsInvalid()
    {
        AssertInvalid(CrmCallsListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-25"), Filter("dateTo", "2026-09-24")
        ])));
    }

    [Fact]
    public void Create_ParsesSortFieldsIgnoringCase()
    {
        Result<CrmCallsListQuery> result = CrmCallsListQueryFactory.Create(Request(sorts:
        [
            Sort("studentName", false), Sort("CALLTYPENAME"), Sort("answerTypeName", false), Sort("mustPayDate"),
            Sort("callDate", false)
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal([
            new CrmCallSortField(ECrmCallSortField.StudentName, false),
            new CrmCallSortField(ECrmCallSortField.CallTypeName, true),
            new CrmCallSortField(ECrmCallSortField.AnswerTypeName, false),
            new CrmCallSortField(ECrmCallSortField.MustPayDate, true),
            new CrmCallSortField(ECrmCallSortField.CallDate, false)
        ], result.Value.SortFields);
    }

    [Theory]
    [InlineData("callConversation")]
    [InlineData("0")]
    [InlineData("99")]
    public void Create_InvalidSortField_IsInvalid(string fieldName)
    {
        AssertInvalid(CrmCallsListQueryFactory.Create(Request(sorts: [Sort(fieldName)])));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_IsInvalid(int offset, int rowsCount)
    {
        AssertInvalid(CrmCallsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1000)]
    public void Create_PageAtTheLimits_IsValid(int offset, int rowsCount)
    {
        Assert.True(CrmCallsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)).IsSuccess);
    }
}
