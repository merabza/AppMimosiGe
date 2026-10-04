using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Xunit;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class GroupFillReportsTests
{
    private static string Text(List<object?> row)
    {
        return string.Join("|", row);
    }

    //"header: row; row" per section
    private static string[] Sections(ReportTable table)
    {
        return
        [
            .. table.Sections.Select(s => $"{s.Header}: {string.Join("; ", s.Rows.Select(Text))}")
        ];
    }

    private static void AssertColumns(ReportTable table, params (string Name, string Caption, string Type)[] columns)
    {
        Assert.Equal(columns, table.Columns.Select(c => (c.Name, c.Caption, c.Type)));
    }

    // r08: the groups whose students are fewer than the places, a section per group size (by the size's id), the
    // groups by code; a full group is not listed
    [Fact]
    public void LessSizeGroups_ListsTheGroupsWithFreePlacesBySize()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(1, "C1", statusName: "S2").Students(1, 1, 2, 3)
            .Group(2, "A1").Students(2, 4, 5, 6, 7)
            .Group(3, "B1", 2, "Art", 1, "Two", 2).Students(3, 8)
            .Group(4, "A2").Students(4, 9)
            .Group(5, "D1", sizeId: 1, sizeName: "Two", size: 2).Students(5, 10, 11).Build();

        // Act
        ReportTable table = GroupFillReports.LessSizeGroups(snapshot);

        // Assert
        AssertColumns(table, ("groupCode", "ჯგუფი", "text"), ("course", "საგანი", "text"),
            ("studentStatus", "მოსწავლის სტატუსი", "text"), ("studentsCount", "რაოდენობა", "wholeNumber"));
        Assert.Equal(["Two: B1|Art|S1|1", "Four: A2|Math|S1|1; C1|Math|S2|3"], Sections(table));
        Assert.All(table.Sections, s => Assert.Null(s.Footer));
        Assert.Empty(table.FooterRows);
    }

    // a group code repeats in another academic year: by the group's id
    [Fact]
    public void LessSizeGroups_SameCode_ByGroupId()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder().Group(5, "A1", 2, "Art").Students(5, 1)
            .Group(3, "A1").Students(3, 2).Build();

        // Act & Assert
        Assert.Equal(["Four: A1|Math|S1|1; A1|Art|S1|1"], Sections(GroupFillReports.LessSizeGroups(snapshot)));
    }

    // r09: the pairs of one course, status and size whose students fit in one group, each pair once (the first by
    // code); another course, status or size, a full group and a pair that does not fit are not listed
    [Fact]
    public void Optimization_ListsThePairsThatFitInOneGroupOnce()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(1, "D1").Students(1, 1, 2)
            .Group(2, "B1").Students(2, 3, 4, 5)
            .Group(3, "A1").Students(3, 6)
            .Group(4, "C1").Students(4, 7, 8)
            .Group(5, "E1", 2, "Art").Students(5, 9)
            .Group(6, "F1", statusId: 2).Students(6, 10)
            .Group(7, "G1", sizeId: 3).Students(7, 11)
            .Group(8, "H1").Students(8, 12, 13, 14, 15).Build();

        // Act
        ReportTable table = GroupFillReports.Optimization(snapshot);

        // Assert
        AssertColumns(table, ("firstGroupCode", "პირველი ჯგუფი", "text"), ("secondGroupCode", "მეორე ჯგუფი", "text"),
            ("size", "ზომა", "wholeNumber"), ("studentsInFirstGroup", "მოსწავლეები პირველ ჯგუფში", "wholeNumber"),
            ("studentsInSecondGroup", "მოსწავლეები მეორე ჯგუფში", "wholeNumber"));
        Assert.Equal([": A1|B1|4|1|3; A1|C1|4|1|2; A1|D1|4|1|2; C1|D1|4|2|2"], Sections(table));
        Assert.Empty(table.FooterRows);
    }

    // a repeated code: the pair is ordered by the groups' ids
    [Fact]
    public void Optimization_SameCode_FirstIsTheSmallerId()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder().Group(7, "A1").Students(7, 1)
            .Group(3, "A1").Students(3, 2, 3).Build();

        // Act & Assert
        Assert.Equal([": A1|A1|4|2|1"], Sections(GroupFillReports.Optimization(snapshot)));
    }

    // r23: every group, a section per size, the rows by students, status, course and code; the fill without
    // decimals; the footer: groups, average load (2 decimals) and fill (2 decimals) of all groups
    [Fact]
    public void GroupSizesAnalysis_ListsEveryGroupWithItsFill()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(1, "G1", statusName: "S2").Students(1, 1, 2)
            .Group(2, "G2").Students(2, 3)
            .Group(3, "G4").Students(3, 4, 5)
            .Group(4, "G3", 2, "Art").Students(4, 6, 7)
            .Group(5, "G6").Students(5, 8, 9)
            .Group(6, "G5", sizeId: 1, sizeName: "Two", size: 2).Students(6, 10, 11).Build();

        // Act
        ReportTable table = GroupFillReports.GroupSizesAnalysis(snapshot);

        // Assert
        AssertColumns(table, ("groupCode", "ჯგუფი", "text"), ("course", "საგანი", "text"),
            ("studentStatus", "მოსწავლის სტატუსი", "text"), ("studentsCount", "რაოდენობა", "wholeNumber"),
            ("fill", "შევსების %", "text"));
        Assert.Equal([
            "Two: G5|Math|S1|2|100%",
            "Four: G2|Math|S1|1|25%; G3|Art|S1|2|50%; G4|Math|S1|2|50%; G6|Math|S1|2|50%; G1|Math|S2|2|50%"
        ], Sections(table));
        Assert.Equal([
            "ჯგუფების რაოდენობა:|6|||", "საშუალო დატვირთვა:|1.83|||", "შევსების % სშ:|50.00%|||"
        ], table.FooterRows.Select(Text));
    }

    // Access's Percent and Fixed formats round half away from zero: 1/8 = 12.5% → 13%, 9/8 = 1.125 → 1.13
    [Fact]
    public void GroupSizesAnalysis_RoundsHalfAwayFromZero()
    {
        // Arrange
        GroupsSnapshotBuilder builder = new GroupsSnapshotBuilder().Group(1, "A1", size: 8).Students(1, 1, 2);
        for (int id = 2; id <= 8; id++)
        {
            builder.Group(id, $"B{id}", size: 100).Students(id, id + 10);
        }

        // Act
        ReportTable table = GroupFillReports.GroupSizesAnalysis(builder.Build());

        // Assert
        Assert.Equal("25%", Assert.Single(table.Sections).Rows.Single(r => (string?)r[0] == "A1")[4]);
        Assert.Equal("1%", Assert.Single(table.Sections).Rows.Single(r => (string?)r[0] == "B2")[4]);
        Assert.Equal("1.13", table.FooterRows[1][1]);
        Assert.Equal("1.27%", table.FooterRows[2][1]);
        Assert.Equal("13%",
            Assert.Single(Assert.Single(GroupFillReports.GroupSizesAnalysis(new GroupsSnapshotBuilder()
                .Group(1, "A1", size: 8).Students(1, 1).Build()).Sections).Rows)[4]);
        Assert.Equal("0.13%", GroupFillReports.GroupSizesAnalysis(new GroupsSnapshotBuilder()
            .Group(1, "A1", size: 800).Students(1, 1).Build()).FooterRows[2][1]);
    }

    // no groups: the count is 0, the averages are empty; a group without places has no fill
    [Fact]
    public void GroupSizesAnalysis_NoGroupsOrNoPlaces_HasNoAverages()
    {
        // Act
        ReportTable empty = GroupFillReports.GroupSizesAnalysis(new GroupsSnapshotBuilder().Build());
        ReportTable noPlaces = GroupFillReports.GroupSizesAnalysis(new GroupsSnapshotBuilder()
            .Group(1, "A1", size: 0).Students(1, 1).Build());

        // Assert
        Assert.Empty(empty.Sections);
        Assert.Equal(["ჯგუფების რაოდენობა:|0|||", "საშუალო დატვირთვა:||||", "შევსების % სშ:||||"],
            empty.FooterRows.Select(Text));
        Assert.Equal(["Four: A1|Math|S1|1|"], Sections(noPlaces));
        Assert.Equal(["ჯგუფების რაოდენობა:|1|||", "საშუალო დატვირთვა:|1.00|||", "შევსების % სშ:||||"],
            noPlaces.FooterRows.Select(Text));
    }

    // r24: the under-filled groups of a course, status and size that need fewer groups (2 groups, 3 students in
    // places of 4 → 1 group); a section per kind with its own counts, the rows: group, its teachers, each student
    // once by name. A full group of the kind is not counted
    [Fact]
    public void GroupsOptimization_ListsTheKindsThatNeedFewerGroups()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(1, "B1").Students(1, 2, 1).Teacher(1, 6).Teacher(1, 5).Teacher(1, 5)
            .Group(2, "A1").Students(2, 3).Teacher(2, 5)
            .Group(3, "F1").Students(3, 4, 5, 6, 7).Teacher(3, 5)
            .Group(4, "C1", 2, "Art").Students(4, 8, 9).Teacher(4, 5)
            .Group(5, "D1", 2, "Art").Students(5, 10, 11, 12).Teacher(5, 5)
            .Group(6, "E1", 3, "Chess").Students(6, 13).Teacher(6, 5).Build();

        // Act
        ReportTable table = GroupFillReports.GroupsOptimization(snapshot);

        // Assert
        AssertColumns(table, ("groupCode", "ჯგუფი", "text"), ("teachers", "მასწავლებელი", "text"),
            ("student", "მოსწავლე", "text"));
        Assert.Equal([
            "Math · S1 · Four · ჯგუფები: 2 · მოსწავლეები: 3: " +
            "A1|TLast5 TFirst5 / T5|SLast3 SFirst3 / 6.003; " +
            "B1|TLast5 TFirst5 / T5; TLast6 TFirst6 / T6|SLast1 SFirst1 / 6.001; " +
            "B1|TLast5 TFirst5 / T5; TLast6 TFirst6 / T6|SLast2 SFirst2 / 6.002"
        ], Sections(table));
        Assert.Empty(table.FooterRows);
    }

    // students that fill whole groups exactly: 2 groups of 2 in places of 4 need 1 group, 5 students need 2
    [Fact]
    public void GroupsOptimization_CountsTheGroupsTheStudentsNeed()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(1, "A1").Students(1, 1, 2).Teacher(1, 5)
            .Group(2, "A2").Students(2, 3, 4).Teacher(2, 5)
            .Group(3, "B1", 2, "Art").Students(3, 5, 6).Teacher(3, 5)
            .Group(4, "B2", 2, "Art").Students(4, 7, 8, 9).Teacher(4, 5).Build();

        // Act & Assert
        Assert.Equal(["Math · S1 · Four · ჯგუფები: 2 · მოსწავლეები: 4"],
            GroupFillReports.GroupsOptimization(snapshot).Sections.Select(s => s.Header));
    }

    // equal codes in a kind (another academic year): by the group's id
    [Fact]
    public void GroupsOptimization_SameCode_ByGroupId()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder().Group(9, "A1").Students(9, 1).Teacher(9, 5)
            .Group(4, "A1").Students(4, 2).Teacher(4, 6).Build();

        // Act & Assert
        Assert.Equal([
            "A1|TLast6 TFirst6 / T6|SLast2 SFirst2 / 6.002", "A1|TLast5 TFirst5 / T5|SLast1 SFirst1 / 6.001"
        ], Assert.Single(GroupFillReports.GroupsOptimization(snapshot).Sections).Rows.Select(Text));
    }

    // the sections by course name, course id, status name, status id and size id
    [Fact]
    public void GroupsOptimization_SectionsByCourseStatusAndSize()
    {
        // Arrange
        GroupsSnapshotBuilder builder = new();
        Kind('A', 2, "Math", 1, "S1", 2);
        Kind('B', 1, "Math", 1, "S1", 2);
        Kind('C', 1, "Math", 3, "S0", 2);
        Kind('D', 1, "Math", 2, "S0", 2);
        Kind('E', 1, "Math", 1, "S1", 1);
        Kind('F', 3, "Art", 1, "S1", 2);

        // Act
        ReportTable table = GroupFillReports.GroupsOptimization(builder.Build());

        // Assert
        Assert.Equal(["F1", "D1", "C1", "E1", "B1", "A1"], table.Sections.Select(s => s.Rows[0][0]));
        return;

        //two under-filled groups of a kind, codes "<kind>1" and "<kind>2", one student each
        void Kind(char kind, int courseId, string courseName, int statusId, string statusName, int sizeId)
        {
            for (int group = 1; group <= 2; group++)
            {
                int id = (kind - 'A') * 10 + group;
                builder.Group(id, $"{kind}{group}", courseId, courseName, sizeId, "Size", 4, statusId, statusName)
                    .Students(id, id).Teacher(id, 5);
            }
        }
    }
}
