using System;
using AppMimosiGe.Application.TeacherContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.TeacherContracts;

public sealed class TeacherContractsListQueryFactoryTests
{
    private static readonly DateTime Today = new(2026, 9, 30, 0, 0, 0, DateTimeKind.Unspecified);

    private static FilterSortRequest Request(ColumnFilter[]? filters = null, SortField[]? sorts = null, int offset = 0,
        int rowsCount = 10)
    {
        return new FilterSortRequest(offset, rowsCount, filters, sorts);
    }

    private static ColumnFilter Filter(string fieldName, string? value)
    {
        return new ColumnFilter { FieldName = fieldName, Value = value };
    }

    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllWithAccessDefaultSort()
    {
        Result<TeacherContractsListQuery> result = TeacherContractsListQueryFactory.Create(Request(), Today);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ActiveOn);
        Assert.Null(result.Value.Search);
        Assert.Equal([
            new TeacherContractSortField(ETeacherContractSortField.ContractNumber, true),
            new TeacherContractSortField(ETeacherContractSortField.TeacherName, true),
            new TeacherContractSortField(ETeacherContractSortField.SalarySchemeName, true)
        ], result.Value.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<TeacherContractsListQuery> result = TeacherContractsListQueryFactory.Create(
            Request([Filter("activeOnly", "true"), Filter("search", "  T3.0 ")], offset: 20, rowsCount: 30),
            Today.AddHours(15));

        Assert.True(result.IsSuccess);
        Assert.Equal(Today, result.Value.ActiveOn);
        Assert.Equal("T3.0", result.Value.Search);
        Assert.Equal(20, result.Value.Offset);
        Assert.Equal(30, result.Value.RowsCount);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_ActiveOnlyNotTrue_ListsAll(string? value)
    {
        Result<TeacherContractsListQuery> result =
            TeacherContractsListQueryFactory.Create(Request([Filter("activeOnly", value)]), Today);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ActiveOn);
    }

    [Fact]
    public void Create_EmptySearch_MeansNoSearch()
    {
        Result<TeacherContractsListQuery> result =
            TeacherContractsListQueryFactory.Create(Request([Filter("search", "  ")]), Today);

        Assert.Null(result.Value.Search);
    }

    [Theory]
    [InlineData("activeOnly", "1")]
    [InlineData("activeOnly", "yes")]
    [InlineData("academicYearId", "11")]
    public void Create_UnknownFilterOrValue_Fails(string fieldName, string value)
    {
        Result<TeacherContractsListQuery> result =
            TeacherContractsListQueryFactory.Create(Request([Filter(fieldName, value)]), Today);

        Assert.Equal(TeacherContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("teacherName", ETeacherContractSortField.TeacherName)]
    [InlineData("salarySchemeName", ETeacherContractSortField.SalarySchemeName)]
    [InlineData("pensionScheme", ETeacherContractSortField.PensionScheme)]
    [InlineData("indEnt", ETeacherContractSortField.IndEnt)]
    [InlineData("fixedAmount", ETeacherContractSortField.FixedAmount)]
    [InlineData("contractEndDate", ETeacherContractSortField.ContractEndDate)]
    [InlineData("contractDate", ETeacherContractSortField.ContractDate)]
    [InlineData("contractNumber", ETeacherContractSortField.ContractNumber)]
    public void Create_ParsesSortFields(string fieldName, ETeacherContractSortField expected)
    {
        Result<TeacherContractsListQuery> result = TeacherContractsListQueryFactory.Create(
            Request(sorts: [new SortField(false, fieldName)]), Today);

        Assert.Equal([new TeacherContractSortField(expected, false)], result.Value.SortFields);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("1")]
    [InlineData("rsCountryId")]
    //Enum.TryParse combines comma-separated values (1 | 8 = 9), which is no sort field
    [InlineData("1,8")]
    public void Create_UnknownSortField_Fails(string fieldName)
    {
        Result<TeacherContractsListQuery> result = TeacherContractsListQueryFactory.Create(
            Request(sorts: [new SortField(true, fieldName)]), Today);

        Assert.Equal(TeacherContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_Fails(int offset, int rowsCount)
    {
        Result<TeacherContractsListQuery> result =
            TeacherContractsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount), Today);

        Assert.Equal(TeacherContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Create_PageSizeLimits_AreAllowed(int rowsCount)
    {
        Assert.True(TeacherContractsListQueryFactory.Create(Request(rowsCount: rowsCount), Today).IsSuccess);
    }
}
