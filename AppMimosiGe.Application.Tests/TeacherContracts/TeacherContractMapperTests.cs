using System;
using AppMimosiGe.Application.TeacherContracts.Validation;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.TeacherContracts.TeacherContractTestData;

namespace AppMimosiGe.Application.Tests.TeacherContracts;

public sealed class TeacherContractMapperTests
{
    [Fact]
    public void TimeOnlyBaseDate_IsTheAccessTimeDate()
    {
        Assert.Equal(new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified),
            TeacherContractMapper.TimeOnlyBaseDate);
    }

    [Fact]
    public void ToDateTime_Null_IsNull()
    {
        Assert.Null(TeacherContractMapper.ToDateTime(null));
    }

    [Fact]
    public void ToDateTime_Time_IsTheTimeOnTheAccessDate()
    {
        // Act
        var result = TeacherContractMapper.ToDateTime(new TimeOnly(9, 15, 30));

        // Assert
        Assert.Equal(new DateTime(1899, 12, 30, 9, 15, 30, DateTimeKind.Unspecified), result);
    }

    [Fact]
    public void ApplyFields_CopiesEveryEditableField()
    {
        // Arrange
        var teacherContract = new TeacherContract();
        TeacherContractRequest request = ValidRequest(workHoursStart: new TimeOnly(12, 0),
            workHoursEnd: new TimeOnly(18, 30),
            contractEndDate: new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Unspecified), fixedAmount: 850.5m,
            description: "ხელფასი");

        // Act
        TeacherContractMapper.ApplyFields(teacherContract, request);

        // Assert
        Assert.Equal("T3.01", teacherContract.ContractNumber);
        Assert.Equal(ContractDate, teacherContract.ContractDate);
        Assert.Equal(1, teacherContract.TeacherHumanId);
        Assert.Equal("GE00TB0000000000000000", teacherContract.BankAccount);
        Assert.Equal("TBCBGE22", teacherContract.BankAccountCode);
        Assert.True(teacherContract.PensionScheme);
        Assert.False(teacherContract.IndEnt);
        Assert.Equal(1, teacherContract.RsQuoteTypeId);
        Assert.Equal(2, teacherContract.RsCountryId);
        Assert.Equal(850.5m, teacherContract.FixedAmount);
        Assert.True(teacherContract.NextMonth);
        Assert.Equal("ხელფასი", teacherContract.Description);
        Assert.Equal(4, teacherContract.SalarySchemaByHoursId);
        Assert.Equal(5, teacherContract.WorkHourGroupId);
        Assert.Equal(new DateTime(1899, 12, 30, 12, 0, 0, DateTimeKind.Unspecified), teacherContract.WorkHoursStart);
        Assert.Equal(new DateTime(1899, 12, 30, 18, 30, 0, DateTimeKind.Unspecified), teacherContract.WorkHoursEnd);
        Assert.Equal(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Unspecified), teacherContract.ContractEndDate);
    }

    // the dates are days: a time part coming with the request is dropped
    [Fact]
    public void ApplyFields_DropsTheTimeOfTheDates()
    {
        // Arrange
        var teacherContract = new TeacherContract();
        var request = new TeacherContractRequest
        {
            ContractNumber = "T3.01",
            ContractDate = new DateTime(2026, 9, 15, 10, 45, 0, DateTimeKind.Unspecified),
            ContractEndDate = new DateTime(2027, 6, 30, 23, 59, 0, DateTimeKind.Unspecified)
        };

        // Act
        TeacherContractMapper.ApplyFields(teacherContract, request);

        // Assert
        Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified), teacherContract.ContractDate);
        Assert.Equal(new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Unspecified), teacherContract.ContractEndDate);
    }

    [Fact]
    public void ApplyFields_TrimsTextsAndStoresBlankTextsAsNull()
    {
        // Arrange
        var teacherContract = new TeacherContract();
        TeacherContractRequest request = ValidRequest(bankAccount: "  GE00TB0000000000000000 ", bankAccountCode: "   ",
            description: " პრემია ");

        // Act
        TeacherContractMapper.ApplyFields(teacherContract, request);

        // Assert
        Assert.Equal("GE00TB0000000000000000", teacherContract.BankAccount);
        Assert.Null(teacherContract.BankAccountCode);
        Assert.Equal("პრემია", teacherContract.Description);
    }

    [Fact]
    public void ApplyFields_KeepsId()
    {
        // Arrange
        var teacherContract = new TeacherContract { Id = 42 };

        // Act
        TeacherContractMapper.ApplyFields(teacherContract, ValidRequest());

        // Assert
        Assert.Equal(42, teacherContract.Id);
    }
}
