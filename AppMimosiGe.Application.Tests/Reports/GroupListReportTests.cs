using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppMimosiGe.Application.Reports.Groups.Models;
using AppMimosiGe.Application.Reports.Models;
using Xunit;
using static AppMimosiGe.Application.Tests.Reports.GroupsSnapshotBuilder;

namespace AppMimosiGe.Application.Tests.Reports;

public sealed class GroupListReportTests
{
    private static string Text(List<object?> row)
    {
        return string.Join("|", row.Select(c => c switch
        {
            DateTime d => d.ToString("MM-dd", CultureInfo.InvariantCulture),
            float or decimal => Convert.ToDecimal(c, CultureInfo.InvariantCulture)
                .ToString("0.####", CultureInfo.InvariantCulture),
            _ => c
        }));
    }

    //"header: row; row" per section
    private static string[] Sections(ReportTable table)
    {
        return
        [
            .. table.Sections.Select(s => $"{s.Header}: {string.Join("; ", s.Rows.Select(Text))}")
        ];
    }

    //groups A1 (Math, teacher 5, students 1 and 2) and B1 (Art, teachers 6 and 5, student 2)
    private static GroupsSnapshot Snapshot()
    {
        return new GroupsSnapshotBuilder()
            .Group(2, "B1", 2, "Art", statusName: "S2").Student(2, 2, 4f, 24m, 0.5f, Day(2026, 9, 10))
            .Teacher(2, 6, "Hours", Day(2026, 9, 2)).Teacher(2, 5, "Fixed", Day(2026, 9, 3))
            .Group(1, "A1").Student(1, 2, 8f, 48.5m, 1f, Day(2026, 9, 5)).Student(1, 1)
            .Teacher(1, 5, "Fixed").Build();
    }

    // every group by code: its course, status and teachers (scheme, start) in the header, the students by name
    [Fact]
    public void Groups_NoFilters_ListsEveryGroup()
    {
        // Act
        ReportTable table = GroupListReport.Groups(Snapshot(), null, null, null);

        // Assert
        Assert.Equal([
            ("student", "მოსწავლე", "text"), ("fourWeekHours", "4 კვირის საათები", "number"),
            ("fourWeekFee", "4 კვირის გადასახადი", "number"), ("hoursCoefficient", "საათის კოეფიციენტი", "number"),
            ("startDate", "თარიღიდან", "date")
        ], table.Columns.Select(c => (c.Name, c.Caption, c.Type)));
        Assert.Equal([
            "ჯგუფი: A1 · საგანი: Math · მოსწავლის სტატუსი: S1 · მასწავლებელი: TLast5 TFirst5 / T5, Fixed, " +
            "თარიღიდან: 01.09.2026: SLast1 SFirst1 / 6.001|8|48|1|09-01; SLast2 SFirst2 / 6.002|8|48.5|1|09-05",
            "ჯგუფი: B1 · საგანი: Art · მოსწავლის სტატუსი: S2 · მასწავლებელი: TLast5 TFirst5 / T5, Fixed, " +
            "თარიღიდან: 03.09.2026; TLast6 TFirst6 / T6, Hours, თარიღიდან: 02.09.2026: " +
            "SLast2 SFirst2 / 6.002|4|24|0.5|09-10"
        ], Sections(table));
        Assert.All(table.Sections, s => Assert.Null(s.Footer));
        Assert.Empty(table.FooterRows);
    }

    // a teacher: only the groups the teacher teaches in, and only that teacher in the header
    [Fact]
    public void Groups_Teacher_ShowsTheTeachersGroups()
    {
        // Act
        ReportTable table = GroupListReport.Groups(Snapshot(), 6, null, null);

        // Assert
        Assert.Equal([
            "ჯგუფი: B1 · საგანი: Art · მოსწავლის სტატუსი: S2 · მასწავლებელი: TLast6 TFirst6 / T6, Hours, " +
            "თარიღიდან: 02.09.2026: SLast2 SFirst2 / 6.002|4|24|0.5|09-10"
        ], Sections(table));
    }

    // a course: only its groups
    [Fact]
    public void Groups_Course_ShowsTheCoursesGroups()
    {
        Assert.Equal(["A1"], GroupListReport.Groups(Snapshot(), null, 1, null).Sections
            .Select(s => s.Header!.Split(' ')[1]));
    }

    // a student: only the student's rows, and only the groups the student is in
    [Fact]
    public void Groups_Student_ShowsTheStudentsRows()
    {
        // Act
        ReportTable table = GroupListReport.Groups(Snapshot(), null, null, 1);

        // Assert
        Assert.Equal(["SLast1 SFirst1 / 6.001|8|48|1|09-01"], Assert.Single(table.Sections).Rows.Select(Text));
    }

    // the filters together: a teacher of the course who does not teach the student leaves nothing
    [Fact]
    public void Groups_FiltersTogether_AllMustMatch()
    {
        Assert.Single(GroupListReport.Groups(Snapshot(), 5, 2, 2).Sections);
        Assert.Empty(GroupListReport.Groups(Snapshot(), 6, 1, null).Sections);
        Assert.Empty(GroupListReport.Groups(Snapshot(), 6, null, 1).Sections);
    }

    // equal codes (another academic year): by the group's id; one contract's two rows (Access's double
    // registration): by the row's id; one teacher's two rows: by start, then by the row's id
    [Fact]
    public void Groups_Ties_ByIdAndStart()
    {
        // Arrange
        GroupsSnapshot snapshot = new GroupsSnapshotBuilder()
            .Group(9, "A1", 2, "Art").Student(9, 3).Teacher(9, 5)
            .Group(4, "A1").Student(4, 3, 2f, rowId: 8).Student(4, 3, 1f, rowId: 7)
            .Teacher(4, 5, "Second", Day(2026, 9, 7)).Teacher(4, 5, "First", Day(2026, 9, 6))
            .Teacher(4, 5, "Fourth", Day(2026, 9, 8), 11).Teacher(4, 5, "Third", Day(2026, 9, 8), 10).Build();

        // Act
        ReportTable table = GroupListReport.Groups(snapshot, null, null, null);

        // Assert
        Assert.Equal(["Math", "Art"], table.Sections.Select(s => s.Header!.Split(' ')[4]));
        Assert.Equal(["1", "2"], table.Sections[0].Rows.Select(r => Text(r).Split('|')[1]));
        Assert.EndsWith("First, თარიღიდან: 06.09.2026; TLast5 TFirst5 / T5, Second, თარიღიდან: 07.09.2026; " +
                        "TLast5 TFirst5 / T5, Third, თარიღიდან: 08.09.2026; TLast5 TFirst5 / T5, Fourth, " +
                        "თარიღიდან: 08.09.2026", table.Sections[0].Header, StringComparison.Ordinal);
    }
}
