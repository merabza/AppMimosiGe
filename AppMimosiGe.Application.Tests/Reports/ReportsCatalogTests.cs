using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Reports;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGe.Application.Reports.Schedule;
using AppMimosiGe.Application.Reports.Schedule.RoomOverlaps;
using AppMimosiGe.Application.Reports.Schedule.RoomsAgenda;
using AppMimosiGe.Application.Reports.Schedule.StudentDoubleCourses;
using AppMimosiGe.Application.Reports.Schedule.StudentOverlaps;
using AppMimosiGe.Application.Reports.Schedule.StudentsAgenda;
using AppMimosiGe.Application.Reports.Schedule.TeacherDoubleGroups;
using AppMimosiGe.Application.Reports.Schedule.TeacherOverlaps;
using AppMimosiGe.Application.Reports.Schedule.TeachersAgenda;
using AppMimosiGe.Application.Reports.Schedule.UsedDayTimes;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class ReportsCatalogTests
{
    private static readonly DateTime Date = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);

    //part 16; r35TeacherLineOver is not ported (Q9)
    private static readonly string[] Part16Keys =
    [
        "r03RoomsAgenda", "r04StudentsAgenda", "r05TeachersAgenda", "r06RoomOver", "r07UsedDayTimes",
        "r18TeacherOver", "r19StudentOver", "r20StudentDoubleCources", "r21TeacherDoubleGroups"
    ];

    private static void AssertKeys(IEnumerable<string> actual, params string[] expected)
    {
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Definitions_AreThePart16ReportsInAccessOrder()
    {
        Assert.Equal(Part16Keys, ReportsCatalog.Definitions.Select(d => d.Key));
    }

    [Fact]
    public void Definitions_KeysAreUniqueIgnoringCase()
    {
        Assert.Equal(ReportsCatalog.Definitions.Count,
            ReportsCatalog.Definitions.Select(d => d.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Definitions_HaveTitlesDescriptionsAndKnownCategories()
    {
        HashSet<string> categoryKeys = [.. ReportsCatalog.Categories.Select(c => c.Key)];
        Assert.All(ReportsCatalog.Definitions, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.False(string.IsNullOrWhiteSpace(d.Description));
            Assert.NotEmpty(d.CategoryKeys);
            //"all" holds every report by itself
            Assert.DoesNotContain(ReportsCatalog.AllCategoryKey, d.CategoryKeys);
            Assert.All(d.CategoryKeys, key => Assert.Contains(key, categoryKeys));
        });
    }

    // the schedule reports show the state for one date: "თარიღისთვის", required
    [Fact]
    public void Definitions_Part16ReportsHaveTheRequiredEndDate()
    {
        Assert.All(ReportsCatalog.Definitions,
            d => Assert.Equal(new ReportParameter(ReportParameterNames.EndDate, "თარიღისთვის", true),
                Assert.Single(d.Parameters)));
    }

    // Access's ReportCategories, in the order of their ids
    [Fact]
    public void Categories_AreTheAccessCategories()
    {
        Assert.Equal([
            new ReportCategory("all", "ყველა"), new ReportCategory("schedule", "გაკვეთილების ცხრილი"),
            new ReportCategory("groups", "ჯგუფები"), new ReportCategory("comments", "კომენტარები"),
            new ReportCategory("lessons", "გაკვეთილები"), new ReportCategory("finance", "ფინანსები"),
            new ReportCategory("checks", "შემოწმება")
        ], ReportsCatalog.Categories);
    }

    // Access's ReportsByCategories for the part 16 reports
    [Theory]
    [InlineData("r03RoomsAgenda", false)]
    [InlineData("r04StudentsAgenda", false)]
    [InlineData("r05TeachersAgenda", false)]
    [InlineData("r06RoomOver", true)]
    [InlineData("r07UsedDayTimes", true)]
    [InlineData("r18TeacherOver", true)]
    [InlineData("r19StudentOver", true)]
    [InlineData("r20StudentDoubleCources", true)]
    [InlineData("r21TeacherDoubleGroups", true)]
    public void Definitions_CategoriesAreThoseOfAccess(string key, bool isCheck)
    {
        string[] expected = isCheck ? ["schedule", "checks"] : ["schedule"];
        Assert.Equal(expected, ReportsCatalog.Find(key)!.CategoryKeys);
    }

    [Theory]
    [InlineData("r03RoomsAgenda")]
    [InlineData("R03ROOMSAGENDA")]
    public void Find_IgnoresTheCase(string key)
    {
        Assert.Equal("r03RoomsAgenda", ReportsCatalog.Find(key)?.Key);
    }

    [Theory]
    [InlineData("r35TeacherLineOver")]
    [InlineData("catalog")]
    [InlineData("")]
    public void Find_UnknownKey_IsNull(string key)
    {
        Assert.Null(ReportsCatalog.Find(key));
    }

    // only the reports whose app claim the user has, and only the categories that have one of them
    [Fact]
    public void ForClaims_ShowsTheClaimedReportsAndTheirCategories()
    {
        // Act
        ReportCatalogResponse catalog =
            ReportsCatalog.ForClaims(new HashSet<string> { "r06RoomOver", "CheckPayments", "r03RoomsAgenda" });

        // Assert
        AssertKeys(catalog.Reports.Select(r => r.Key), "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories.Select(c => c.Key), "all", "schedule", "checks");
        AssertKeys(catalog.Categories[0].ReportKeys, "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories[1].ReportKeys, "r03RoomsAgenda", "r06RoomOver");
        AssertKeys(catalog.Categories[2].ReportKeys, "r06RoomOver");
        Assert.Equal("შემოწმება", catalog.Categories[2].Name);
        ReportInfoResponse report = catalog.Reports[0];
        Assert.Equal("ოთახების ცხრილი", report.Title);
        Assert.Equal(ReportsCatalog.Find("r03RoomsAgenda")!.Description, report.Description);
        Assert.Equal(new ReportParameterInfoResponse(ReportParameterNames.EndDate, "თარიღისთვის", true),
            Assert.Single(report.Parameters));
    }

    // claims are compared exactly, as the carcass stores them
    [Fact]
    public void ForClaims_ClaimInAnotherCase_DoesNotShowTheReport()
    {
        Assert.Empty(ReportsCatalog.ForClaims(new HashSet<string> { "R03ROOMSAGENDA" }).Reports);
    }

    [Fact]
    public void ForClaims_NoClaims_IsEmpty()
    {
        // Act
        ReportCatalogResponse catalog = ReportsCatalog.ForClaims(new HashSet<string>());

        // Assert
        Assert.Empty(catalog.Reports);
        Assert.Empty(catalog.Categories);
    }

    // every report calls its own query handler with the end date
    [Theory]
    [InlineData("r03RoomsAgenda", typeof(RoomsAgendaReportQuery))]
    [InlineData("r04StudentsAgenda", typeof(StudentsAgendaReportQuery))]
    [InlineData("r05TeachersAgenda", typeof(TeachersAgendaReportQuery))]
    [InlineData("r06RoomOver", typeof(RoomOverlapsReportQuery))]
    [InlineData("r07UsedDayTimes", typeof(UsedDayTimesReportQuery))]
    [InlineData("r18TeacherOver", typeof(TeacherOverlapsReportQuery))]
    [InlineData("r19StudentOver", typeof(StudentOverlapsReportQuery))]
    [InlineData("r20StudentDoubleCources", typeof(StudentDoubleCoursesReportQuery))]
    [InlineData("r21TeacherDoubleGroups", typeof(TeacherDoubleGroupsReportQuery))]
    public async Task Run_CallsTheReportsOwnHandlerWithTheEndDate(string key, Type queryType)
    {
        // Arrange
        var services = new ServiceCollection();
        List<object> queries = [];
        Dictionary<Type, ReportTable> tables = [];
        Add<RoomsAgendaReportQuery>();
        Add<StudentsAgendaReportQuery>();
        Add<TeachersAgendaReportQuery>();
        Add<RoomOverlapsReportQuery>();
        Add<UsedDayTimesReportQuery>();
        Add<TeacherOverlapsReportQuery>();
        Add<StudentOverlapsReportQuery>();
        Add<StudentDoubleCoursesReportQuery>();
        Add<TeacherDoubleGroupsReportQuery>();
        await using ServiceProvider provider = services.BuildServiceProvider();

        // Act
        Result<ReportTable> result = await ReportsCatalog.Find(key)!.Run(
            new ReportParametersRequest(null, Date, null, null, null), provider, CancellationToken.None);

        // Assert
        Assert.Same(tables[queryType], result.Value);
        object query = Assert.Single(queries);
        Assert.IsType(queryType, query);
        Assert.Equal(Date, ((ScheduleReportQuery)query).Date);
        return;

        void Add<TQuery>() where TQuery : IQuery<ReportTable>
        {
            ReportTable table = ReportTable.Flat([new ReportColumnResponse(typeof(TQuery).Name, "", "text")], []);
            tables[typeof(TQuery)] = table;
            var handler = new Mock<IQueryHandler<TQuery, ReportTable>>();
            handler.Setup(h => h.Handle(It.IsAny<TQuery>(), It.IsAny<CancellationToken>()))
                .Callback<TQuery, CancellationToken>((q, _) => queries.Add(q)).ReturnsAsync(table);
            services.AddSingleton(handler.Object);
        }
    }
}
