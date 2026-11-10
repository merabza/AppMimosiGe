using System;
using System.Collections.Generic;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class BankTransactionReportsTests
{
    private static readonly DateTime Day = new(2026, 11, 9, 10, 30, 0, DateTimeKind.Unspecified);

    private static BankTransactionRowResponse Row(int id, int day, decimal amount, decimal allocated,
        bool ignored = false)
    {
        return new BankTransactionRowResponse(id, $"bank {id}", Day.AddDays(day), amount, $"payer {id}", $"purpose {id}", 0, false,
            ignored, allocated, false);
    }

    // only the not fully allocated ones (ignored ones not), oldest first, with the remainder and the totals
    [Fact]
    public void Unallocated_ListsTheRemaindersWithTotals()
    {
        ReportTable table = BankTransactionReports.Unallocated([
            Row(5, 1, 100m, 40m), Row(3, 0, 50m, 0m), Row(4, 0, 70m, 70m), Row(6, -1, 30m, 0m, true),
            Row(2, 1, 20m, 0m)
        ]);

        Assert.Equal(["transactionDate", "bankName", "partnerName", "purpose", "amount", "allocated", "remainder"],
            table.Columns.ConvertAll(c => c.Name));
        Assert.Equal([ReportColumnTypes.Date, ReportColumnTypes.Text, ReportColumnTypes.Text, ReportColumnTypes.Text,
            ReportColumnTypes.Number, ReportColumnTypes.Number, ReportColumnTypes.Number
        ], table.Columns.ConvertAll(c => c.Type));
        List<List<object?>> expected =
        [
            [Day.Date, "bank 3", "payer 3", "purpose 3", 50m, 0m, 50m],
            [Day.Date.AddDays(1), "bank 2", "payer 2", "purpose 2", 20m, 0m, 20m],
            [Day.Date.AddDays(1), "bank 5", "payer 5", "purpose 5", 100m, 40m, 60m]
        ];
        Assert.Equal(expected, Assert.Single(table.Sections).Rows);
        List<List<object?>> footers = [[ReportFooters.TotalCaption, 3, null, null, 170m, 40m, 130m]];
        Assert.Equal(footers, table.FooterRows);
    }
}
