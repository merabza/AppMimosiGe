using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class FinanceReportsTests
{
    private static readonly DateTime September = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime October = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly Dictionary<int, string> MonthNames = new()
    {
        [9] = "სექტემბერი", [10] = "ოქტომბერი"
    };

    private static string Text(List<object?> row)
    {
        return string.Join("|", row.Select(c => c switch
        {
            float or decimal => Convert.ToDecimal(c, CultureInfo.InvariantCulture)
                .ToString("0.####", CultureInfo.InvariantCulture),
            _ => c
        }));
    }

    //synthetic people: "Last<id>", "First<id>", personal id 0100000000<id>
    private static Dictionary<int, Debtor> Humans(params int[] ids)
    {
        return ids.ToDictionary(id => id, id => new Debtor($"Last{id}", $"First{id}", $"0100000000{id}"));
    }

    // a payment counts for the student and for the payer, once for one who is both; a person's payments are summed;
    // by last name, first name and personal id; the footer counts the people
    [Fact]
    public void BlackList_SumsThePaymentsOfStudentsAndPayers()
    {
        // Arrange
        Dictionary<int, Debtor> humans = Humans(1, 2, 3, 4);
        humans[4] = new Debtor("Last2", "First2", "0100000000-");
        humans[3] = new Debtor("Last2", "Ann", "01000000003");
        var data = new BlackListData([
            new DebtPayment(1, 1, 2, 100m), new DebtPayment(2, 3, 3, 50.5m), new DebtPayment(3, 4, 2, -20m)
        ], humans);

        // Act
        ReportTable table = FinanceReports.BlackList(data);

        // Assert
        Assert.Equal([
            ("lastName", "გვარი", "text"), ("firstName", "სახელი", "text"), ("personalId", "პირადი ნომერი", "text"),
            ("debt", "დავალიანება", "number")
        ], table.Columns.Select(c => (c.Name, c.Caption, c.Type)));
        Assert.Equal([
            "Last1|First1|01000000001|100", "Last2|Ann|01000000003|50.5", "Last2|First2|0100000000-|-20",
            "Last2|First2|01000000002|80"
        ], Assert.Single(table.Sections).Rows.Select(Text));
        Assert.Equal(["სულ:|4||"], table.FooterRows.Select(Text));
    }

    [Fact]
    public void BlackList_NoPayments_IsEmpty()
    {
        // Act
        ReportTable table = FinanceReports.BlackList(new BlackListData([], new Dictionary<int, Debtor>()));

        // Assert
        Assert.Empty(Assert.Single(table.Sections).Rows);
        Assert.Equal(["სულ:|0||"], table.FooterRows.Select(Text));
    }

    private static SalaryDetailRow Detail(int id, DateTime month, int teacherId, string groupCode, float hours,
        decimal cost, decimal amount, int? groupId = null)
    {
        return new SalaryDetailRow(id, month, teacherId, $"TLast{teacherId}", $"TName{teacherId}", $"T{teacherId}",
            groupId ?? groupCode[0], groupCode, "Math", hours, cost, amount);
    }

    // a section per month and teacher (month, then the teacher's name and number), the groups by code; the section's
    // footer: hours, the average cost of an hour (4 decimals) and the amount
    [Fact]
    public void TeacherSalaryByGroups_SectionsPerMonthAndTeacher()
    {
        // Arrange
        SalaryDetailRow[] details =
        [
            Detail(1, October, 5, "B1", 4f, 15m, 60m), Detail(2, September, 6, "A1", 1.5f, 10m, 15m),
            Detail(3, September, 5, "C1", 2f, 12m, 24m), Detail(4, September, 5, "A1", 1.5f, 11m, 16.5m)
        ];

        // Act
        ReportTable table = FinanceReports.TeacherSalaryByGroups(details, MonthNames);

        // Assert
        Assert.Equal([
            ("groupCode", "ჯგუფი", "text"), ("course", "საგანი", "text"), ("hours", "საათები", "number"),
            ("hourCost", "ფასი", "number"), ("amount", "ღირებულება", "number")
        ], table.Columns.Select(c => (c.Name, c.Caption, c.Type)));
        Assert.Equal([
            "სექტემბერი 2026 · მასწავლებელი: TLast5 TName5 / T5", "სექტემბერი 2026 · მასწავლებელი: TLast6 TName6 / T6",
            "ოქტომბერი 2026 · მასწავლებელი: TLast5 TName5 / T5"
        ], table.Sections.Select(s => s.Header));
        Assert.Equal(["A1|Math|1.5|11|16.5", "C1|Math|2|12|24"], table.Sections[0].Rows.Select(Text));
        Assert.Equal(["სულ:||3.5|11.5714|40.5", "სულ:||1.5|10|15", "სულ:||4|15|60"],
            table.Sections.Select(s => Text(s.Footer!)));
        Assert.Empty(table.FooterRows);
    }

    // no hours: no average (Access showed #Div/0!); a month without a name shows its number
    [Fact]
    public void TeacherSalaryByGroups_ZeroHoursAndUnknownMonth()
    {
        // Act
        ReportTable table = FinanceReports.TeacherSalaryByGroups(
            [Detail(1, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified), 5, "A1", 0f, 0m, 30m)],
            MonthNames);

        // Assert
        AssertSingleSection(table, "11 2026 · მასწავლებელი: TLast5 TName5 / T5", "სულ:||0||30");
    }

    // one group twice in a section (two salary headers): by the group's id, then by the detail's id
    [Fact]
    public void TeacherSalaryByGroups_SameGroupCode_ByGroupThenDetail()
    {
        // Arrange
        SalaryDetailRow[] details =
        [
            Detail(9, September, 5, "A1", 3f, 10m, 30m, 2), Detail(8, September, 5, "A1", 2f, 10m, 20m, 2),
            Detail(7, September, 5, "A1", 1f, 10m, 10m, 3), Detail(6, September, 5, "A1", 4f, 10m, 40m, 1)
        ];

        // Act & Assert
        Assert.Equal(["4", "2", "3", "1"], Assert.Single(FinanceReports.TeacherSalaryByGroups(details, MonthNames)
            .Sections).Rows.Select(r => Text(r).Split('|')[2]));
    }

    private static void AssertSingleSection(ReportTable table, string header, string footer)
    {
        Assert.Equal(header, Assert.Single(table.Sections).Header);
        Assert.Equal(footer, Text(table.Sections[0].Footer!));
    }
}
