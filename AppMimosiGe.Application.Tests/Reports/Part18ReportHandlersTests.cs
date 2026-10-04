using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Comments.DailyComments;
using AppMimosiGe.Application.Reports.Comments.Models;
using AppMimosiGe.Application.Reports.Finance.BlackList;
using AppMimosiGe.Application.Reports.Finance.Models;
using AppMimosiGe.Application.Reports.Finance.TeacherSalaryByGroups;
using AppMimosiGe.Application.Reports.Groups.GroupList;
using AppMimosiGe.Application.Reports.Groups.GroupSizesAnalysis;
using AppMimosiGe.Application.Reports.Groups.GroupsOptimization;
using AppMimosiGe.Application.Reports.Groups.LessSizeGroups;
using AppMimosiGe.Application.Reports.Groups.MergeableGroupPairs;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.WorkTime.Models;
using AppMimosiGe.Application.Reports.WorkTime.TimeSheet;
using Moq;
using SystemTools.SharedKernel;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.GroupsSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class Part18ReportHandlersTests
{
    private static readonly DateTime Date = Day(2026, 10, 2);

    private static readonly Dictionary<int, string> MonthNames = new() { [9] = "სექტემბერი", [10] = "ოქტომბერი" };

    private readonly Mock<IReportsRepository> _repository = new();

    //two under-filled groups of one kind (r24 lists them) with a teacher each
    private static readonly GroupsSnapshot Groups = new GroupsSnapshotBuilder().Group(1, "A1").Students(1, 1)
        .Teacher(1, 5).Group(2, "B1").Students(2, 2, 3).Teacher(2, 6).Build();

    private static void AssertSameTable(ReportTable expected, Result<ReportTable> actual)
    {
        Assert.Equal(expected.Columns, actual.Value.Columns);
        Assert.Equal(expected.Sections.Select(s => s.Header), actual.Value.Sections.Select(s => s.Header));
        Assert.Equal(expected.Sections.Select(s => s.Rows), actual.Value.Sections.Select(s => s.Rows));
        Assert.Equal(expected.Sections.Select(s => s.Footer), actual.Value.Sections.Select(s => s.Footer));
        Assert.Equal(expected.FooterRows, actual.Value.FooterRows);
    }

    private void SetupGroups()
    {
        _repository.Setup(r => r.GetGroups(Date, It.IsAny<CancellationToken>())).ReturnsAsync(Groups);
    }

    // the group reports load the active groups of the date and build the report from them
    [Fact]
    public async Task GroupReports_LoadTheDatesGroups()
    {
        // Arrange
        SetupGroups();

        // Act
        Result<ReportTable> lessSize = await new LessSizeGroupsReportQueryHandler(_repository.Object)
            .Handle(new LessSizeGroupsReportQuery(Date), CancellationToken.None);
        Result<ReportTable> pairs = await new MergeableGroupPairsReportQueryHandler(_repository.Object)
            .Handle(new MergeableGroupPairsReportQuery(Date), CancellationToken.None);
        Result<ReportTable> sizes = await new GroupSizesAnalysisReportQueryHandler(_repository.Object)
            .Handle(new GroupSizesAnalysisReportQuery(Date), CancellationToken.None);
        Result<ReportTable> optimization = await new GroupsOptimizationReportQueryHandler(_repository.Object)
            .Handle(new GroupsOptimizationReportQuery(Date), CancellationToken.None);

        // Assert
        AssertSameTable(GroupFillReports.LessSizeGroups(Groups), lessSize);
        AssertSameTable(GroupFillReports.Optimization(Groups), pairs);
        AssertSameTable(GroupFillReports.GroupSizesAnalysis(Groups), sizes);
        AssertSameTable(GroupFillReports.GroupsOptimization(Groups), optimization);
        Assert.NotEmpty(Assert.Single(optimization.Value.Sections).Rows);
        _repository.Verify(r => r.GetGroups(Date, It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    // r10 passes its filters to the report
    [Fact]
    public async Task GroupList_PassesTheFilters()
    {
        // Arrange
        SetupGroups();

        // Act
        Result<ReportTable> result = await new GroupListReportQueryHandler(_repository.Object)
            .Handle(new GroupListReportQuery(Date, 6, 1, 2), CancellationToken.None);

        // Assert
        AssertSameTable(GroupListReport.Groups(Groups, 6, 1, 2), result);
        Assert.Equal("ჯგუფი: B1", Assert.Single(result.Value.Sections).Header![..9]);
    }

    // r01: the whole month of the date (from its first day to the next month's), the lesson's teacher
    [Fact]
    public async Task DailyComments_LoadsTheDatesMonth()
    {
        // Arrange
        CommentLesson lesson = new(1, Day(2026, 10, 5).AddHours(10), "A1", "Math",
            new SchedulePerson("T", "F", "T5"), null, null, []);
        _repository.Setup(r => r.GetCommentLessons(Day(2026, 10, 1), Day(2026, 11, 1), 5,
            It.IsAny<CancellationToken>())).ReturnsAsync([lesson]);

        // Act
        Result<ReportTable> result = await new DailyCommentsReportQueryHandler(_repository.Object)
            .Handle(new DailyCommentsReportQuery(Date.AddDays(20).AddHours(15), 5), CancellationToken.None);

        // Assert
        AssertSameTable(CommentReports.DailyComments([lesson]), result);
        Assert.Single(result.Value.Sections);
    }

    [Fact]
    public async Task BlackList_LoadsTheDesperateDebts()
    {
        // Arrange
        var data = new BlackListData([new DebtPayment(1, 1, 1, 5m)],
            new Dictionary<int, Debtor> { [1] = new("L", "F", "01000000001") });
        _repository.Setup(r => r.GetDesperateDebts(It.IsAny<CancellationToken>())).ReturnsAsync(data);

        // Act
        Result<ReportTable> result = await new BlackListReportQueryHandler(_repository.Object)
            .Handle(new BlackListReportQuery(), CancellationToken.None);

        // Assert
        AssertSameTable(FinanceReports.BlackList(data), result);
        Assert.Single(result.Value.Sections[0].Rows);
    }

    // r25: the salary lines' months from the start date's month to the end date's month, the teacher
    [Fact]
    public async Task TeacherSalaryByGroups_LoadsTheMonthsOfThePeriod()
    {
        // Arrange
        SalaryDetailRow detail = new(1, Day(2026, 9, 1), 5, "L", "F", "T5", 1, "A1", "Math", 2f, 10m, 20m);
        _repository.Setup(r => r.GetSalaryDetails(Day(2026, 9, 1), Day(2026, 10, 1), 5,
            It.IsAny<CancellationToken>())).ReturnsAsync([detail]);
        _repository.Setup(r => r.GetMonthNames(It.IsAny<CancellationToken>())).ReturnsAsync(MonthNames);

        // Act
        Result<ReportTable> result = await new TeacherSalaryByGroupsReportQueryHandler(_repository.Object)
            .Handle(new TeacherSalaryByGroupsReportQuery(Day(2026, 9, 15), Date, 5), CancellationToken.None);

        // Assert
        AssertSameTable(FinanceReports.TeacherSalaryByGroups([detail], MonthNames), result);
        Assert.Equal("სექტემბერი 2026 · მასწავლებელი: L F / T5", Assert.Single(result.Value.Sections).Header);
    }

    // r36: the period's days up to the end date's next midnight
    [Fact]
    public async Task TimeSheet_LoadsThePeriodsDays()
    {
        // Arrange
        var data = new WorkTimeData([new WorkTimeLesson(1, 5, Day(2026, 9, 20).AddHours(10), null, 1f)], [],
            new Dictionary<int, SchedulePerson> { [5] = new("L", "F", "T5") });
        _repository.Setup(r => r.GetWorkTime(Day(2026, 9, 15), Day(2026, 10, 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
        _repository.Setup(r => r.GetMonthNames(It.IsAny<CancellationToken>())).ReturnsAsync(MonthNames);

        // Act
        Result<ReportTable> result = await new TimeSheetReportQueryHandler(_repository.Object)
            .Handle(new TimeSheetReportQuery(Day(2026, 9, 15), Date), CancellationToken.None);

        // Assert
        AssertSameTable(TimeSheetReport.TimeSheet(data, MonthNames), result);
        Assert.Equal("სექტემბერი 2026", Assert.Single(result.Value.Sections).Header);
    }
}
