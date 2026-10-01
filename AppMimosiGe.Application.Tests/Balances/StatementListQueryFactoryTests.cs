using System;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Balances;

public sealed class StatementListQueryFactoryTests
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

    private static void AssertInvalid(Result<StatementListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(BalanceErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithoutFilters_IsTheWholeStatement()
    {
        Result<StatementListQuery> result = StatementListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal(new StatementListQuery(0, 10, null, null, null), result.Value);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<StatementListQuery> result = StatementListQueryFactory.Create(Request([
            Filter("studentContractId", " 12 "), Filter("dateFrom", "2026-09-01"), Filter("dateTo", "2026-09-30")
        ], [], 20, 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(new StatementListQuery(20, 30, 12, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified)), result.Value);
    }

    // an empty value removes the filter, as the SPA sends it
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<StatementListQuery> result = StatementListQueryFactory.Create(Request([
            Filter("studentContractId", ""), Filter("dateFrom", " "), Filter("dateTo", null)
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(new StatementListQuery(0, 10, null, null, null), result.Value);
    }

    [Fact]
    public void Create_SameDayFromAndTo_IsValid()
    {
        Result<StatementListQuery> result = StatementListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-15"), Filter("dateTo", "2026-09-15")
        ]));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsInvalid()
    {
        AssertInvalid(StatementListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-16"), Filter("dateTo", "2026-09-15")
        ])));
    }

    // the running total depends on the statement order, so no other order is taken
    [Fact]
    public void Create_SortField_IsInvalid()
    {
        AssertInvalid(StatementListQueryFactory.Create(Request(sorts: [new SortField(true, "operationDate")])));
    }

    [Theory]
    [InlineData("studentContractId", "abc")]
    [InlineData("studentContractId", "-1")]
    [InlineData("studentContractId", "1.5")]
    [InlineData("dateFrom", "15.09.2026")]
    [InlineData("dateTo", "2026-9-15")]
    [InlineData("dateTo", "2026-09-15T10:00")]
    [InlineData("bankAccountId", "1")]
    [InlineData("StudentContractId", "1")]
    public void Create_UnknownFieldOrBadValue_IsInvalid(string fieldName, string value)
    {
        AssertInvalid(StatementListQueryFactory.Create(Request([Filter(fieldName, value)])));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_BadPage_IsInvalid(int offset, int rowsCount)
    {
        AssertInvalid(StatementListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 1000)]
    public void Create_PageLimits_AreValid(int offset, int rowsCount)
    {
        Assert.True(StatementListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)).IsSuccess);
    }
}
