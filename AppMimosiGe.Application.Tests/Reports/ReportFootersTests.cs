using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportFootersTests
{
    private static List<ReportColumnResponse> Columns(int count)
    {
        return [.. Enumerable.Range(1, count).Select(i => new ReportColumnResponse($"c{i}", $"C{i}", "text"))];
    }

    // Access's =Count(*): the caption in the first column, the count in the second, the other cells empty
    [Fact]
    public void Count_FillsTheRowUpToTheColumnCount()
    {
        Assert.Equal(["სულ:", 7, null, null], ReportFooters.Count(Columns(4), 7));
    }

    [Fact]
    public void Count_TwoColumns_HasNoEmptyCells()
    {
        Assert.Equal(["სულ:", 0], ReportFooters.Count(Columns(2), 0));
    }

    [Fact]
    public void TotalCaption_IsTheAccessCaption()
    {
        Assert.Equal("სულ:", ReportFooters.TotalCaption);
    }
}
