using System;
using AppMimosiGe.Application.CrmCalls.Validation;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.CrmCalls;

public sealed class CrmCallMapperTests
{
    // the call date keeps its time (Access default Now()), the "must pay by" date is a day
    [Fact]
    public void ApplyFields_CopiesTheFieldsKeepsTheCallTimeAndDropsTheMustPayTime()
    {
        // Arrange
        var crmCall = new CrmCall { CcId = 5, CallConversation = "old" };

        // Act
        CrmCallMapper.ApplyFields(crmCall,
            new CrmCallRequest
            {
                StudentContractId = 12,
                CallTypeId = 2,
                CallDate = new DateTime(2026, 9, 24, 19, 48, 29, DateTimeKind.Unspecified),
                AnswerTypeId = 3,
                CallConversation = " will pay ",
                MustPayDate = new DateTime(2026, 10, 8, 23, 59, 59, DateTimeKind.Unspecified)
            });

        // Assert
        Assert.Equal(5, crmCall.CcId);
        Assert.Equal(12, crmCall.StudentContractId);
        Assert.Equal(2, crmCall.CallTypeId);
        Assert.Equal(new DateTime(2026, 9, 24, 19, 48, 29, DateTimeKind.Unspecified), crmCall.CallDate);
        Assert.Equal(3, crmCall.AnswerTypeId);
        Assert.Equal("will pay", crmCall.CallConversation);
        Assert.Equal(new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Unspecified), crmCall.MustPayDate);
    }

    // a cleared "must pay by" date removes the stored one
    [Fact]
    public void ApplyFields_WithoutMustPayDate_ClearsIt()
    {
        // Arrange
        var crmCall = new CrmCall
        {
            MustPayDate = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Unspecified), CallConversation = "old"
        };

        // Act
        CrmCallMapper.ApplyFields(crmCall,
            new CrmCallRequest
            {
                StudentContractId = 12,
                CallTypeId = 1,
                CallDate = new DateTime(2026, 9, 24, 19, 48, 0, DateTimeKind.Unspecified),
                AnswerTypeId = 1
            });

        // Assert
        Assert.Null(crmCall.MustPayDate);
        Assert.Null(crmCall.CallConversation);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" will pay \n on Monday ", "will pay \n on Monday")]
    public void NormalizeText_KeepsTrimmedTextAndEmptyAsNull(string? value, string? expected)
    {
        Assert.Equal(expected, CrmCallMapper.NormalizeText(value));
    }
}
