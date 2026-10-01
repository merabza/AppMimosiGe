using System;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcass.Application.Crud.Models;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Groups;

public sealed class GroupsListQueryFactoryTests
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

    private static SortField Sort(string fieldName, bool ascending = true)
    {
        return new SortField(ascending, fieldName);
    }

    [Fact]
    public void Create_WithoutFiltersAndSorts_ListsAllGroupsByCode()
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request(), Today.AddHours(15));

        Assert.True(result.IsSuccess);
        GroupsListQuery query = result.Value;
        Assert.Equal(Today, query.Today);
        Assert.Equal(EGroupFindMethod.Group, query.FindMethod);
        Assert.Null(query.AcademicYearId);
        Assert.Null(query.State);
        Assert.Null(query.CourseId);
        Assert.Null(query.GroupSizeId);
        Assert.Null(query.StudentStatusId);
        Assert.Null(query.Search);
        Assert.Equal([new GroupSortField(EGroupSortField.GroupCode, true)], query.SortFields);
    }

    [Fact]
    public void Create_ParsesFiltersAndPage()
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request([
            Filter("findMethod", "Teacher"), Filter("academicYearId", "11"), Filter("state", "voided"),
            Filter("courseId", "6"), Filter("groupSizeId", "2"), Filter("studentStatusId", "10"),
            Filter("search", "  ალფა ")
        ], offset: 20, rowsCount: 30), Today);

        Assert.True(result.IsSuccess);
        GroupsListQuery query = result.Value;
        Assert.Equal(EGroupFindMethod.Teacher, query.FindMethod);
        Assert.Equal(11, query.AcademicYearId);
        Assert.Equal(EGroupState.Voided, query.State);
        Assert.Equal(6, query.CourseId);
        Assert.Equal(2, query.GroupSizeId);
        Assert.Equal(10, query.StudentStatusId);
        Assert.Equal("ალფა", query.Search);
        Assert.Equal(20, query.Offset);
        Assert.Equal(30, query.RowsCount);
    }

    [Theory]
    [InlineData("active", EGroupState.Active)]
    [InlineData("Active", EGroupState.Active)]
    [InlineData("VOIDED", EGroupState.Voided)]
    public void Create_ParsesState(string value, EGroupState expected)
    {
        Assert.Equal(expected, GroupsListQueryFactory.Create(Request([Filter("state", value)]), Today).Value.State);
    }

    // empty filter values mean no filter; an empty find method is the default one
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_EmptyValues_MeanNoFilter(string? value)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request([
            Filter("findMethod", value), Filter("academicYearId", value), Filter("state", value),
            Filter("courseId", value), Filter("groupSizeId", value), Filter("studentStatusId", value),
            Filter("search", value)
        ]), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(EGroupFindMethod.Group, result.Value.FindMethod);
        Assert.Null(result.Value.AcademicYearId);
        Assert.Null(result.Value.State);
        Assert.Null(result.Value.CourseId);
        Assert.Null(result.Value.GroupSizeId);
        Assert.Null(result.Value.StudentStatusId);
        Assert.Null(result.Value.Search);
    }

    [Theory]
    [InlineData("findMethod", "room")]
    [InlineData("findMethod", "1")]
    [InlineData("state", "open")]
    [InlineData("state", "0")]
    [InlineData("academicYearId", "x")]
    [InlineData("academicYearId", "-1")]
    [InlineData("courseId", "1.5")]
    [InlineData("groupSizeId", "two")]
    [InlineData("studentStatusId", "+3")]
    [InlineData("unknown", "1")]
    public void Create_InvalidFilter_Fails(string fieldName, string value)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request([Filter(fieldName, value)]), Today);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 1001)]
    public void Create_InvalidPage_Fails(int offset, int rowsCount)
    {
        Result<GroupsListQuery> result =
            GroupsListQueryFactory.Create(Request(offset: offset, rowsCount: rowsCount), Today);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Create_PageSizeAtTheLimits_IsAccepted(int rowsCount)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request(rowsCount: rowsCount), Today);

        Assert.True(result.IsSuccess);
        Assert.Equal(rowsCount, result.Value.RowsCount);
    }

    // Access sorted the teacher and student find lists by the person's name
    [Theory]
    [InlineData("teacher", EGroupSortField.TeacherName)]
    [InlineData("student", EGroupSortField.StudentName)]
    public void Create_PersonFindMethods_SortByNameCodeAndStart(string findMethod, EGroupSortField nameField)
    {
        Result<GroupsListQuery> result =
            GroupsListQueryFactory.Create(Request([Filter("findMethod", findMethod)]), Today);

        Assert.Equal([
            new GroupSortField(nameField, true), new GroupSortField(EGroupSortField.GroupCode, true),
            new GroupSortField(EGroupSortField.StartDate, true)
        ], result.Value.SortFields);
    }

    [Fact]
    public void Create_ParsesSortFields()
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(
            Request(sorts: [Sort("courseName"), Sort("ActiveStudentsCount", false)]), Today);

        Assert.Equal([
            new GroupSortField(EGroupSortField.CourseName, true),
            new GroupSortField(EGroupSortField.ActiveStudentsCount, false)
        ], result.Value.SortFields);
    }

    // a column that the find method does not show can not sort its list
    [Theory]
    [InlineData("group", "studentName")]
    [InlineData("group", "startDate")]
    [InlineData("group", "endDate")]
    [InlineData("teacher", "studentName")]
    [InlineData("teacher", "activeStudentsCount")]
    [InlineData("student", "teacherName")]
    [InlineData("student", "activeStudentsCount")]
    public void Create_SortFieldOfAnotherFindMethod_Fails(string findMethod, string sortField)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(
            Request([Filter("findMethod", findMethod)], [Sort(sortField)]), Today);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("teacher", "teacherName")]
    [InlineData("teacher", "endDate")]
    [InlineData("student", "studentName")]
    [InlineData("student", "startDate")]
    [InlineData("group", "teacherName")]
    [InlineData("student", "voidDate")]
    public void Create_SortFieldOfTheFindMethod_IsAccepted(string findMethod, string sortField)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(
            Request([Filter("findMethod", findMethod)], [Sort(sortField)]), Today);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("0")]
    [InlineData("")]
    public void Create_UnknownSortField_Fails(string sortField)
    {
        Result<GroupsListQuery> result = GroupsListQueryFactory.Create(Request(sorts: [Sort(sortField)]), Today);

        Assert.Equal(GroupErrors.FilterSortRequestIsInvalid.Code, result.Error.Code);
    }
}
