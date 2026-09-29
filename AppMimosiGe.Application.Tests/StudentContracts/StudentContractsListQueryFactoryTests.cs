using System.Linq;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.StudentContracts;

public sealed class StudentContractsListQueryFactoryTests
{
    private static FilterSortRequest Request(ColumnFilter[]? filters = null, SortField[]? sorts = null,
        int offset = 0, int rowsCount = 10)
    {
        return new FilterSortRequest(offset, rowsCount, filters, sorts);
    }

    [Fact]
    public void Create_WithoutFiltersAndSorts_UsesAccessDefaultSort()
    {
        Result<StudentContractsListQuery> result = StudentContractsListQueryFactory.Create(Request());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AcademicYearId);
        Assert.Null(result.Value.StudentStatusId);
        Assert.Null(result.Value.Search);
        Assert.Equal(StudentContractsListQueryFactory.DefaultSortFields, result.Value.SortFields);
        Assert.Equal(
        [
            EStudentContractSortField.ContractNumber, EStudentContractSortField.StudentName,
            EStudentContractSortField.PayerName
        ], [.. result.Value.SortFields.Select(s => s.Field)]);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<StudentContractsListQuery> result = StudentContractsListQueryFactory.Create(Request(
        [
            new ColumnFilter { FieldName = "academicYearId", Value = "11" },
            new ColumnFilter { FieldName = "studentStatusId", Value = "3" },
            new ColumnFilter { FieldName = "search", Value = "  6.00 " }
        ], offset: 20, rowsCount: 30));

        Assert.True(result.IsSuccess);
        Assert.Equal(11, result.Value.AcademicYearId);
        Assert.Equal(3, result.Value.StudentStatusId);
        Assert.Equal("6.00", result.Value.Search);
        Assert.Equal(20, result.Value.Offset);
        Assert.Equal(30, result.Value.RowsCount);
    }

    [Fact]
    public void Create_EmptyFilterValues_MeanNoFilter()
    {
        Result<StudentContractsListQuery> result = StudentContractsListQueryFactory.Create(Request(
        [
            new ColumnFilter { FieldName = "academicYearId", Value = "" },
            new ColumnFilter { FieldName = "studentStatusId", Value = null },
            new ColumnFilter { FieldName = "search", Value = "   " }
        ]));

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AcademicYearId);
        Assert.Null(result.Value.StudentStatusId);
        Assert.Null(result.Value.Search);
    }

    [Fact]
    public void Create_ParsesSortFieldsCaseInsensitively()
    {
        Result<StudentContractsListQuery> result = StudentContractsListQueryFactory.Create(Request(sorts:
        [
            new SortField(false, "contractDate"), new SortField(true, "payerName")
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(
        [
            new StudentContractSortField(EStudentContractSortField.ContractDate, false),
            new StudentContractSortField(EStudentContractSortField.PayerName, true)
        ], result.Value.SortFields);
    }

    [Theory]
    [InlineData("academicYearId", "abc")]
    [InlineData("academicYearId", "-1")]
    [InlineData("studentStatusId", "1.5")]
    [InlineData("unknownField", "1")]
    [InlineData(null, "1")]
    public void Create_InvalidFilter_Fails(string? fieldName, string value)
    {
        Result<StudentContractsListQuery> result =
            StudentContractsListQueryFactory.Create(Request([new ColumnFilter { FieldName = fieldName, Value = value }]));

        Assert.True(result.IsFailure);
        Assert.Equal(StudentContractErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("3")]
    [InlineData("99")]
    public void Create_InvalidSortField_Fails(string fieldName)
    {
        Result<StudentContractsListQuery> result =
            StudentContractsListQueryFactory.Create(Request(sorts: [new SortField(true, fieldName)]));

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, StudentContractsListQueryFactory.MaxRowsCount + 1)]
    public void Create_InvalidPage_Fails(int offset, int rowsCount)
    {
        Result<StudentContractsListQuery> result =
            StudentContractsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_MaxRowsCount_IsAllowed()
    {
        Assert.True(StudentContractsListQueryFactory
            .Create(Request(rowsCount: StudentContractsListQueryFactory.MaxRowsCount)).IsSuccess);
    }
}
