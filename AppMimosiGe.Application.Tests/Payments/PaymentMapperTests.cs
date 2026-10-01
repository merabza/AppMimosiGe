using System;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Payments.Validation;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Payments;

public sealed class PaymentMapperTests
{
    // the pay date is a day: Access stored no time (default Date())
    [Fact]
    public void ApplyFields_CopiesTheFieldsAndDropsTheTime()
    {
        // Arrange
        var payment = new Payment { Id = 5, Document = "old", Checked = false };

        // Act
        PaymentMapper.ApplyFields(payment, new PaymentRequest
        {
            StudentContractId = 12,
            PayDate = new DateTime(2026, 9, 15, 23, 59, 59, DateTimeKind.Unspecified),
            Amount = 99.99m,
            Document = null,
            BankAccountId = 8,
            Checked = true
        });

        // Assert
        Assert.Equal(5, payment.Id);
        Assert.Equal(12, payment.StudentContractId);
        Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Unspecified), payment.PayDate);
        Assert.Equal(99.99m, payment.Amount);
        Assert.Null(payment.Document);
        Assert.Equal(8, payment.BankAccountId);
        Assert.True(payment.Checked);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" N 15 ", "N 15")]
    public void NormalizeText_KeepsTrimmedTextAndEmptyAsNull(string? value, string? expected)
    {
        Assert.Equal(expected, PaymentMapper.NormalizeText(value));
    }

    // the key the seeder gives the Admin (MimNewAppClaimsRulesCreator.CheckPaymentsKey) and the SPA checks
    [Fact]
    public void CheckPaymentsClaim_IsTheSeededAppClaimKey()
    {
        Assert.Equal("CheckPayments", PaymentClaims.CheckPayments);
    }
}
