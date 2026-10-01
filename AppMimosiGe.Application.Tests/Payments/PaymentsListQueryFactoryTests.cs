using System;
using AppMimosiGe.Application.Payments.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Payments;

public sealed class PaymentsListQueryFactoryTests
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

    private static void AssertInvalid(Result<PaymentsListQuery> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(PaymentErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    // Access sorted the payments by date and student
    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllPaymentsByDateAndStudent()
    {
        Result<PaymentsListQuery> result = PaymentsListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        PaymentsListQuery query = result.Value;
        Assert.Null(query.StudentContractId);
        Assert.Null(query.BankAccountId);
        Assert.Null(query.DateFrom);
        Assert.Null(query.DateTo);
        Assert.Equal([
            new PaymentSortField(EPaymentSortField.PayDate, true),
            new PaymentSortField(EPaymentSortField.StudentName, true)
        ], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<PaymentsListQuery> result = PaymentsListQueryFactory.Create(Request([
            Filter("studentContractId", "12"), Filter("bankAccountId", " 4 "), Filter("dateFrom", "2026-09-01"),
            Filter("dateTo", "2026-09-30")
        ], offset: 20, rowsCount: 30));

        Assert.True(result.IsSuccess);
        PaymentsListQuery query = result.Value;
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
        Assert.Equal(12, query.StudentContractId);
        Assert.Equal(4, query.BankAccountId);
        Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified), query.DateFrom);
        Assert.Equal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified), query.DateTo);
    }

    // an empty value removes the filter, as the SPA sends it
    [Fact]
    public void Create_EmptyValues_AreNoFilters()
    {
        Result<PaymentsListQuery> result = PaymentsListQueryFactory.Create(Request([
            Filter("studentContractId", ""), Filter("bankAccountId", " "), Filter("dateFrom", null),
            Filter("dateTo", "")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.StudentContractId);
        Assert.Null(result.Value.BankAccountId);
        Assert.Null(result.Value.DateFrom);
        Assert.Null(result.Value.DateTo);
    }

    // one day is a valid range: both ends are included
    [Fact]
    public void Create_SameDayRange_IsValid()
    {
        Result<PaymentsListQuery> result =
            PaymentsListQueryFactory.Create(Request([
                Filter("dateFrom", "2026-09-15"), Filter("dateTo", "2026-09-15")
            ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(result.Value.DateFrom, result.Value.DateTo);
    }

    [Theory]
    [InlineData("studentContractId", "x")]
    [InlineData("studentContractId", "-1")]
    [InlineData("bankAccountId", "1.5")]
    [InlineData("dateFrom", "01.09.2026")]
    [InlineData("dateTo", "2026-13-01")]
    [InlineData("grpId", "1")]
    public void Create_InvalidFilter_IsInvalid(string fieldName, string value)
    {
        AssertInvalid(PaymentsListQueryFactory.Create(Request([Filter(fieldName, value)])));
    }

    [Fact]
    public void Create_DateFromAfterDateTo_IsInvalid()
    {
        AssertInvalid(PaymentsListQueryFactory.Create(Request([
            Filter("dateFrom", "2026-09-16"), Filter("dateTo", "2026-09-15")
        ])));
    }

    [Fact]
    public void Create_ParsesSortFieldsIgnoringCase()
    {
        Result<PaymentsListQuery> result = PaymentsListQueryFactory.Create(Request(sorts:
        [
            Sort("amount", false), Sort("bankName"), Sort("DOCUMENT", false), Sort("checked"), Sort("studentName")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal([
            new PaymentSortField(EPaymentSortField.Amount, false),
            new PaymentSortField(EPaymentSortField.BankName, true),
            new PaymentSortField(EPaymentSortField.Document, false),
            new PaymentSortField(EPaymentSortField.Checked, true),
            new PaymentSortField(EPaymentSortField.StudentName, true)
        ], result.Value.SortFields);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    [InlineData("99")]
    public void Create_InvalidSortField_IsInvalid(string fieldName)
    {
        AssertInvalid(PaymentsListQueryFactory.Create(Request(sorts: [Sort(fieldName)])));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_IsInvalid(int offset, int rowsCount)
    {
        AssertInvalid(PaymentsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1000)]
    public void Create_PageAtTheLimits_IsValid(int offset, int rowsCount)
    {
        Assert.True(PaymentsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount)).IsSuccess);
    }
}
