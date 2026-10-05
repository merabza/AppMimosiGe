using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.GetReportCatalog;
using AppMimosiGe.Application.Reports.GetReportExcel;
using AppMimosiGe.Application.Reports.GetReportLookups;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.RunReport;
using AppMimosiGe.Application.Reports.Schedule;
using AppMimosiGe.Application.Reports.Schedule.Models;
using AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;
using AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;
using AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;
using AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;
using AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;
using AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;
using AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;
using AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;
using AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportHandlersTests
{
    private static readonly DateTime Date = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);

    private static readonly ReportTable Table = new([new ReportColumnResponse("roomName", "ოთახი", "text")],
        [new ReportSectionResponse(null, [["R1"]], null)], [["სულ:"]]);

    private readonly Mock<IReportsRepository> _repository = new();
    private readonly Mock<IQueryHandler<RoomsAgendaReportQuery, ReportTable>> _roomsAgenda = new();
    private readonly List<RoomsAgendaReportQuery> _roomsAgendaQueries = [];

    public ReportHandlersTests()
    {
        _roomsAgenda.Setup(h => h.Handle(It.IsAny<RoomsAgendaReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<RoomsAgendaReportQuery, CancellationToken>((q, _) => _roomsAgendaQueries.Add(q))
            .ReturnsAsync(Table);
    }

    private async Task<Result<ReportResponse>> Run(string key, DateTime? endDate)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_roomsAgenda.Object);
        await using ServiceProvider provider = services.BuildServiceProvider();
        return await new RunReportQueryHandler(provider, _repository.Object).Handle(
            new RunReportQuery(key, new ReportParametersRequest(null, endDate, null, null, null)),
            CancellationToken.None);
    }

    [Fact]
    public async Task RunReport_UnknownKey_IsNotFound()
    {
        // Act
        Result<ReportResponse> result = await Run("r35TeacherLineOver", Date);

        // Assert
        Assert.Equal(ReportErrors.ReportNotFound.Code, result.Error.Code);
        Assert.Empty(_roomsAgendaQueries);
    }

    [Fact]
    public async Task RunReport_MissingEndDate_IsAProblemAndDoesNotRunTheReport()
    {
        // Act
        Result<ReportResponse> result = await Run("r03RoomsAgenda", null);

        // Assert
        Assert.Equal(nameof(ReportErrors.ParameterIsRequired), result.Error.Code);
        Assert.Empty(_roomsAgendaQueries);
    }

    // the report's own handler gets the day; the response adds the catalog's key, title and the parameter line
    [Fact]
    public async Task RunReport_Success_ReturnsTheReportWithTitleAndParameters()
    {
        // Act
        Result<ReportResponse> result = await Run("R03ROOMSAGENDA", Date.AddHours(15.5));

        // Assert
        Assert.True(result.IsSuccess);
        ReportResponse report = result.Value;
        Assert.Equal("r03RoomsAgenda", report.Key);
        Assert.Equal("ოთახების ცხრილი", report.Title);
        Assert.Equal(new ReportParameterValueResponse(ReportParameterNames.EndDate, "თარიღისთვის", "02.10.2026"),
            Assert.Single(report.Parameters));
        Assert.Same(Table.Columns, report.Columns);
        Assert.Same(Table.Sections, report.Sections);
        Assert.Same(Table.FooterRows, report.FooterRows);
        Assert.Equal(Date, Assert.Single(_roomsAgendaQueries).Date);
        //only teacher, course and student parameters need the lookups
        _repository.Verify(r => r.GetLookups(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunReport_ReportFails_ReturnsItsError()
    {
        // Arrange
        Error error = Error.Problem("Broken", "broken");
        _roomsAgenda.Setup(h => h.Handle(It.IsAny<RoomsAgendaReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(error);

        // Act
        Result<ReportResponse> result = await Run("r03RoomsAgenda", Date);

        // Assert
        Assert.Same(error, result.Error);
    }

    [Fact]
    public async Task GetCatalog_ShowsTheReportsOfTheUsersClaims()
    {
        // Arrange
        var claimRights = new Mock<IUserClaimRights>();
        claimRights.Setup(c => c.GetClaims(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "r07UsedDayTimes" });

        // Act
        Result<ReportCatalogResponse> result =
            await new GetReportCatalogQueryHandler(claimRights.Object).Handle(new GetReportCatalogQuery(),
                CancellationToken.None);

        // Assert
        Assert.Equal("r07UsedDayTimes", Assert.Single(result.Value.Reports).Key);
    }

    [Fact]
    public async Task GetLookups_ReturnsTheRepositorysLists()
    {
        // Arrange
        var lookups = new ReportLookupsResponse([], [new LookupItemResponse(3, "Math")], [], []);
        _repository.Setup(r => r.GetLookups(It.IsAny<CancellationToken>())).ReturnsAsync(lookups);

        // Act
        Result<ReportLookupsResponse> result =
            await new GetReportLookupsQueryHandler(_repository.Object).Handle(new GetReportLookupsQuery(),
                CancellationToken.None);

        // Assert
        Assert.Same(lookups, result.Value);
    }

    [Fact]
    public async Task GetExcel_ReportFails_ReturnsItsErrorWithoutAFile()
    {
        // Arrange
        var runReport = new Mock<IQueryHandler<RunReportQuery, ReportResponse>>();
        runReport.Setup(h => h.Handle(It.IsAny<RunReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReportErrors.ReportNotFound);
        var writer = new Mock<IReportExcelWriter>();

        // Act
        Result<ReportFile> result = await new GetReportExcelQueryHandler(runReport.Object, writer.Object).Handle(
            new GetReportExcelQuery("rX", new ReportParametersRequest(null, Date, null, null, null)),
            CancellationToken.None);

        // Assert
        Assert.Equal(ReportErrors.ReportNotFound.Code, result.Error.Code);
        writer.Verify(w => w.Write(It.IsAny<ReportResponse>()), Times.Never);
    }

    // the same report as on the screen, written by the writer, named by the key and the day
    [Fact]
    public async Task GetExcel_Success_WritesTheReport()
    {
        // Arrange
        var report = new ReportResponse("r03RoomsAgenda", "ოთახების ცხრილი", [], Table.Columns, Table.Sections, []);
        var runReport = new Mock<IQueryHandler<RunReportQuery, ReportResponse>>();
        RunReportQuery? query = null;
        runReport.Setup(h => h.Handle(It.IsAny<RunReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<RunReportQuery, CancellationToken>((q, _) => query = q).ReturnsAsync(report);
        byte[] content = [1, 2, 3];
        var writer = new Mock<IReportExcelWriter>();
        writer.Setup(w => w.Write(report)).Returns(content);
        var parameters = new ReportParametersRequest(null, Date.AddHours(9), null, null, null);

        // Act
        Result<ReportFile> result = await new GetReportExcelQueryHandler(runReport.Object, writer.Object).Handle(
            new GetReportExcelQuery("R03ROOMSAGENDA", parameters), CancellationToken.None);

        // Assert
        Assert.Same(content, result.Value.Content);
        Assert.Equal("r03RoomsAgenda_2026-10-02.xlsx", result.Value.FileName);
        Assert.Equal(new RunReportQuery("R03ROOMSAGENDA", parameters), query);
    }

    // every schedule report handler loads the day's schedule and builds its report from it
    [Fact]
    public Task RoomsAgenda_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new RoomsAgendaReportQueryHandler(_repository.Object),
            new RoomsAgendaReportQuery(Date), ScheduleAgendaReports.RoomsAgenda);
    }

    [Fact]
    public Task StudentsAgenda_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new StudentsAgendaReportQueryHandler(_repository.Object),
            new StudentsAgendaReportQuery(Date), ScheduleAgendaReports.StudentsAgenda);
    }

    [Fact]
    public Task TeachersAgenda_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new TeachersAgendaReportQueryHandler(_repository.Object),
            new TeachersAgendaReportQuery(Date), ScheduleAgendaReports.TeachersAgenda);
    }

    [Fact]
    public Task RoomOverlaps_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new RoomOverlapsReportQueryHandler(_repository.Object),
            new RoomOverlapsReportQuery(Date), ScheduleOverlapReports.RoomOverlaps);
    }

    [Fact]
    public Task UsedDayTimes_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new UsedDayTimesReportQueryHandler(_repository.Object),
            new UsedDayTimesReportQuery(Date), ScheduleAgendaReports.UsedDayTimes);
    }

    [Fact]
    public Task TeacherOverlaps_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new TeacherOverlapsReportQueryHandler(_repository.Object),
            new TeacherOverlapsReportQuery(Date), ScheduleOverlapReports.TeacherOverlaps);
    }

    [Fact]
    public Task StudentOverlaps_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new StudentOverlapsReportQueryHandler(_repository.Object),
            new StudentOverlapsReportQuery(Date), ScheduleOverlapReports.StudentOverlaps);
    }

    [Fact]
    public Task StudentDoubleCourses_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new StudentDoubleCoursesReportQueryHandler(_repository.Object),
            new StudentDoubleCoursesReportQuery(Date), ScheduleDoubleReports.StudentDoubleCourses);
    }

    [Fact]
    public Task TeacherDoubleGroups_BuildsTheReportFromTheDaysSchedule()
    {
        return AssertScheduleHandler(new TeacherDoubleGroupsReportQueryHandler(_repository.Object),
            new TeacherDoubleGroupsReportQuery(Date), ScheduleDoubleReports.TeacherDoubleGroups);
    }

    //a schedule on which every report has rows: overlaps, doubles and a two-room group
    private static ScheduleSnapshot BusySchedule()
    {
        return new ScheduleSnapshotBuilder().Group(1, "A1", 3).Group(2, "B1", 3).Lesson(1, 1, 1, "10:00")
            .Lesson(2, 2, 1, "10:30", roomId: 2).Lesson(3, 2, 1, "11:00").Teacher(1, 1, 5).Teacher(2, 2, 5)
            .Teacher(3, 2, 5).Student(1, 1, 20).Student(2, 2, 20).Build();
    }

    private async Task AssertScheduleHandler<TQuery>(IQueryHandler<TQuery, ReportTable> handler, TQuery query,
        Func<ScheduleSnapshot, ReportTable> build) where TQuery : ScheduleReportQuery
    {
        // Arrange
        ScheduleSnapshot schedule = BusySchedule();
        _repository.Setup(r => r.GetSchedule(Date, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);

        // Act
        Result<ReportTable> result = await handler.Handle(query, CancellationToken.None);

        // Assert
        ReportTable expected = build(schedule);
        Assert.NotEmpty(expected.Sections.SelectMany(s => s.Rows));
        Assert.Equal(expected.Columns, result.Value.Columns);
        Assert.Equal(expected.Sections.SelectMany(s => s.Rows), result.Value.Sections.SelectMany(s => s.Rows));
        Assert.Equal(expected.FooterRows, result.Value.FooterRows);
        _repository.Verify(r => r.GetSchedule(Date, It.IsAny<CancellationToken>()), Times.Once);
    }
}
