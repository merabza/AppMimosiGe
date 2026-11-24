using System;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.GroupOrders;

public sealed class GroupOrdersListQueryFactoryTests
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

    private static void AssertInvalid(Result<GroupOrdersListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(GroupOrderErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    //D217: ნომრით, ახალი პირველი; ნომრის გარეშე საწყისი ბრძანებები თარიღის კლებით
    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllOrdersNewestNumberFirst()
    {
        Result<GroupOrdersListQuery> result = GroupOrdersListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        GroupOrdersListQuery query = result.Value;
        Assert.Null(query.AcademicYearId);
        Assert.Null(query.OrderType);
        Assert.Null(query.GroupId);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Null(query.IsPaused);
        Assert.Equal([
            new GroupOrderSortField(EGroupOrderSortField.Number, false),
            new GroupOrderSortField(EGroupOrderSortField.OrderDate, false)
        ], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<GroupOrdersListQuery> result = GroupOrdersListQueryFactory.Create(Request([
            Filter("academicYearId", "11"), Filter("orderType", " 5 "), Filter("groupId", "7"),
            Filter("dateFrom", "2026-09-01"), Filter("dateTo", "2026-09-30"), Filter("isPaused", "true")
        ], offset: 20, rowsCount: 30));

        Assert.True(result.IsSuccess);
        GroupOrdersListQuery query = result.Value;
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
        Assert.Equal(11, query.AcademicYearId);
        Assert.Equal(EGroupOrderType.TransferStudents, query.OrderType);
        Assert.Equal(7, query.GroupId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), query.DateFrom);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), query.DateTo);
        Assert.True(query.IsPaused);
    }

    [Theory]
    [InlineData("0", EGroupOrderType.Initial)]
    [InlineData("10", EGroupOrderType.ChangeWeeklyHours)]
    public void Create_OrderType_AcceptsEveryTypeNumber(string value, EGroupOrderType orderType)
    {
        Result<GroupOrdersListQuery> result =
            GroupOrdersListQueryFactory.Create(Request([Filter("orderType", value)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(orderType, result.Value.OrderType);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("True", true)]
    public void Create_IsPaused_ParsesTheFlag(string value, bool isPaused)
    {
        Result<GroupOrdersListQuery> result =
            GroupOrdersListQueryFactory.Create(Request([Filter("isPaused", value)]));

        Assert.True(result.IsSuccess);
        Assert.Equal(isPaused, result.Value.IsPaused);
    }

    //ცარიელი მნიშვნელობა ფილტრს ხსნის, როგორც SPA აგზავნის
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<GroupOrdersListQuery> result = GroupOrdersListQueryFactory.Create(Request([
            Filter("academicYearId", ""), Filter("orderType", " "), Filter("groupId", null), Filter("dateFrom", ""),
            Filter("dateTo", null), Filter("isPaused", " ")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AcademicYearId);
        Assert.Null(result.Value.OrderType);
        Assert.Null(result.Value.GroupId);
        Assert.Null(result.Value.DateFrom);
        Assert.Null(result.Value.DateTo);
        Assert.Null(result.Value.IsPaused);
    }

    //D216: ერთი დღის შუალედი დასაშვებია
    [Fact]
    public void Create_SameDateFromAndTo_IsValid()
    {
        Result<GroupOrdersListQuery> result = GroupOrdersListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-12-01"), Filter("dateTo", "2026-12-01")
        ]));

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("academicYearId", "abc")]
    [InlineData("academicYearId", "-1")]
    [InlineData("groupId", "1.5")]
    [InlineData("orderType", "11")]
    [InlineData("orderType", "-1")]
    [InlineData("orderType", "CreateGroup")]
    [InlineData("dateFrom", "01.09.2026")]
    [InlineData("dateTo", "2026-13-01")]
    [InlineData("isPaused", "1")]
    [InlineData("isPaused", "yes")]
    [InlineData("number", "26.1")]
    public void Create_InvalidFilter_IsRefused(string fieldName, string value)
    {
        AssertInvalid(GroupOrdersListQueryFactory.Create(Request([Filter(fieldName, value)])));
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsRefused()
    {
        AssertInvalid(GroupOrdersListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-12-02"), Filter("dateTo", "2026-12-01")
        ])));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_IsRefused(int offset, int rowsCount)
    {
        AssertInvalid(GroupOrdersListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)));
    }

    [Fact]
    public void Create_MaxRowsCount_IsValid()
    {
        Assert.True(GroupOrdersListQueryFactory.Create(Request(rowsCount: 1000)).IsSuccess);
    }

    //დალაგების ველი სახელით, რეგისტრის მიუხედავად, მოთხოვნის რიგით
    [Fact]
    public void Create_SortFields_KeepTheirOrderAndDirection()
    {
        Result<GroupOrdersListQuery> result = GroupOrdersListQueryFactory.Create(Request(sorts:
            [Sort("groupCode"), Sort("NUMBER", false), Sort("targetGroupCode")]));

        Assert.True(result.IsSuccess);
        Assert.Equal([
            new GroupOrderSortField(EGroupOrderSortField.GroupCode, true),
            new GroupOrderSortField(EGroupOrderSortField.Number, false),
            new GroupOrderSortField(EGroupOrderSortField.TargetGroupCode, true)
        ], result.Value.SortFields);
    }

    [Theory]
    [InlineData("studentName")]
    [InlineData("0")]
    [InlineData("")]
    public void Create_UnknownSortField_IsRefused(string fieldName)
    {
        AssertInvalid(GroupOrdersListQueryFactory.Create(Request(sorts: [Sort(fieldName)])));
    }
}
