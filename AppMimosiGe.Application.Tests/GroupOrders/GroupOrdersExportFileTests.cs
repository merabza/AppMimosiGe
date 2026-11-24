using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using AppMimosiGe.Application.GroupOrders.Models;
using MimosiGeCore.Domain.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersModeTestData;
using static AppMimosiGe.Application.Tests.GroupOrders.GroupOrdersTestData;

namespace AppMimosiGe.Application.Tests.GroupOrders;

//ვიზარდი 2-ის ფაილი (D195): ბრძანებები ხაზებით და სტრიქონების კავშირებით, სახელი და JSON
public sealed class GroupOrdersExportFileTests
{
    private static readonly DateTime ExportedAt = new(2026, 11, 24, 16, 30, 5, DateTimeKind.Unspecified);

    //ჯგუფი 4-ის საწყისი ბრძანება (Id 2) და შემდეგ დაპაუზებული შექმნის ბრძანება (Id 1, ჯგუფის გარეშე)
    private static (List<GroupOrder> Orders, GroupOrderLinkedRows Rows) StoredOrders()
    {
        Group group = StoredGroup(4, "605");
        GroupOrder initial = StoredInitialOrder(group, 2, 101);
        var paused = new GroupOrder
        {
            Id = 1,
            OrderType = EGroupOrderType.CreateGroup,
            AcademicYearId = 11,
            NumberYear = 26,
            Number = 3,
            IssueDate = Day(11, 25),
            OrderDate = Day(12, 1),
            IsPaused = true,
            GroupCode = "607",
            CourseId = 6,
            GroupSizeId = 2,
            StudentStatusId = 10,
            OrganizationName = "ცოდნის სახლი მიმოსი",
            SignerTitle = "დირექტორი",
            SignerName = "ნატალია ჭყონია",
            CreatedByUserName = "manager",
            CreatedAt = Day(11, 25),
            ChangedByUserName = "admin",
            ChangedAt = Day(11, 26)
        };
        paused.GroupOrderStudents.Add(new GroupOrderStudent
        {
            Id = 200,
            StudentContractId = 22,
            FourWeekHours = 8f,
            FourWeekFee = 48m,
            OneHourFee = 6m,
            HoursCoefficient = 1f,
            Note = "ახალი"
        });
        return ([initial, paused], LinkedRows(group));
    }

    private static GroupOrdersExport Export()
    {
        (List<GroupOrder> orders, GroupOrderLinkedRows rows) = StoredOrders();
        return GroupOrdersExportMapper.ToExport(orders, rows, Day(11, 24), "admin", ExportedAt);
    }

    [Fact]
    public void ToExport_HasTheHeaderAndEveryOrderInIdOrder()
    {
        GroupOrdersExport export = Export();

        Assert.Equal((ExportedAt, "admin", (DateTime?)Day(11, 24)),
            (export.ExportedAt, export.ExportedByUserName, export.TransitionDate));
        Assert.Equal([1, 2], export.Orders.Select(o => o.Id));
        GroupOrderExport paused = export.Orders[0];
        Assert.Equal(
            new GroupOrderExport(1, 1, 11, 26, 3, Day(11, 25), Day(12, 1), true, null, null, "607", 6, 2, 10, null,
                "ცოდნის სახლი მიმოსი", "დირექტორი", "ნატალია ჭყონია", "manager", Day(11, 25), "admin",
                Day(11, 26), paused.Teachers, paused.Students, paused.DayTimePlaces), paused);
        Assert.Equal([new GroupOrderStudentExport(200, 22, 8f, 48m, 6m, 1f, "ახალი", null, null, null)],
            paused.Students);
        Assert.Empty(paused.Teachers);
    }

    //ხაზს მისი სტრიქონის Id აქვს: ვიზარდი 2 კავშირს შლის, ფაილში კი ის რჩება
    [Fact]
    public void ToExport_LinesHaveTheirRows()
    {
        GroupOrderExport initial = Export().Orders[1];

        Assert.Equal((0, (int?)4, "605", (DateTime?)Day(9, 1)),
            (initial.OrderType, initial.GroupId, initial.GroupCode, (DateTime?)initial.OrderDate));
        Assert.Equal([new GroupOrderTeacherExport(101, 5, 8, Day(9, 1), null, 41)], initial.Teachers);
        Assert.Equal(
        [
            new GroupOrderStudentExport(102, 24, 8f, 48m, 6m, 1f, "შენიშვნა", Day(9, 1), null, 42),
            new GroupOrderStudentExport(103, 44, 8f, 48m, 6m, 1f, null, Day(9, 1), Day(9, 15), 43)
        ], initial.Students);
        Assert.Equal([new GroupOrderDayTimePlaceExport(104, 1, 4, 2f, 1, Day(9, 1), null, 44)],
            initial.DayTimePlaces);
    }

    [Fact]
    public void Create_NamesTheFileByTheExportTime()
    {
        Assert.Equal("GroupOrders_20261124163005.json", GroupOrdersExportFile.Create(Export()).FileName);
    }

    //JSON UTF-8-ში, BOM-ის გარეშე, ქართული escape-ის გარეშე; ტიპი რიცხვია
    [Fact]
    public void Create_WritesReadableJson()
    {
        byte[] content = GroupOrdersExportFile.Create(Export()).Content;

        Assert.Equal((byte)'{', content[0]);
        string text = Encoding.UTF8.GetString(content);
        Assert.Contains("\"Note\": \"შენიშვნა\"", text, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(content);
        JsonElement initial = document.RootElement.GetProperty("Orders")[1];
        Assert.Equal(0, initial.GetProperty("OrderType").GetInt32());
        Assert.Equal(42, initial.GetProperty("Students")[0].GetProperty("RowId").GetInt32());
        Assert.Equal("2026-11-24T00:00:00", document.RootElement.GetProperty("TransitionDate").GetString());
    }

    [Theory]
    [InlineData("GroupOrders_20261124163005.json", true)]
    [InlineData("GroupOrders_2026112416300.json", false)]
    [InlineData("GroupOrders_20261124163005.json.bak", false)]
    [InlineData("..\\GroupOrders_20261124163005.json", false)]
    [InlineData("../GroupOrders_20261124163005.json", false)]
    [InlineData("GroupOrders_20261124163005.JSON", false)]
    [InlineData("appsettings.json", false)]
    public void IsFileName_AcceptsOnlyTheWizardFileName(string fileName, bool isFileName)
    {
        Assert.Equal(isFileName, GroupOrdersExportFile.IsFileName(fileName));
    }
}
