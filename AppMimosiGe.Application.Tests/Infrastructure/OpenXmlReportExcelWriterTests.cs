using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AppMimosiGe.Infrastructure.Reports;
using AppMimosiGeShared.Contracts.V1.Responses;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Validation;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

public sealed class OpenXmlReportExcelWriterTests
{
    private static readonly List<ReportColumnResponse> Columns =
    [
        new("roomName", "ოთახი", ReportColumnTypes.Text), new("startTime", "დრო", ReportColumnTypes.Time),
        new("count", "რაოდენობა", ReportColumnTypes.WholeNumber), new("amount", "თანხა", ReportColumnTypes.Number),
        new("day", "თარიღი", ReportColumnTypes.Date), new("moment", "დრო და თარიღი", ReportColumnTypes.DateTime)
    ];

    private static readonly DateTime Day = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly string[] CaptionCells = ["A4", "B4", "C4", "D4", "E4", "F4"];

    private static ReportResponse Report(string key = "r03RoomsAgenda", List<ReportSectionResponse>? sections = null,
        List<List<object?>>? footerRows = null)
    {
        return new ReportResponse(key, "ოთახების ცხრილი",
            [new ReportParameterValueResponse(ReportParameterNames.EndDate, "თარიღისთვის", "02.10.2026")], Columns,
            sections ??
            [
                new ReportSectionResponse(null, [
                    ["R1", new TimeOnly(15, 0), 2, 12.5m, Day, Day.AddHours(10.5)],
                    ["R2", null, 0, null, null, null]
                ], null)
            ], footerRows ?? [["სულ:", null, 2]]);
    }

    private static Worksheet Read(byte[] content, out SpreadsheetDocument document)
    {
        document = SpreadsheetDocument.Open(new MemoryStream(content), false);
        WorkbookPart workbookPart = document.WorkbookPart!;
        return workbookPart.WorksheetParts.Single().Worksheet!;
    }

    private static Cell CellAt(Worksheet worksheet, string reference)
    {
        return worksheet.Descendants<Cell>().Single(c => c.CellReference?.Value == reference);
    }

    private static Row RowAt(Worksheet worksheet, uint index)
    {
        return worksheet.Descendants<Row>().Single(r => r.RowIndex?.Value == index);
    }

    private static string Text(Cell cell)
    {
        return cell.InlineString!.Text!.Text;
    }

    private static uint Style(Cell cell)
    {
        return cell.StyleIndex!.Value;
    }

    private static string? NumberFormat(SpreadsheetDocument document, Cell cell)
    {
        Stylesheet stylesheet = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        CellFormat format = stylesheet.CellFormats!.Elements<CellFormat>().ElementAt((int)Style(cell));
        uint id = format.NumberFormatId!.Value;
        return id == 0
            ? null
            : stylesheet.NumberingFormats?.Elements<NumberingFormat>()
                  .SingleOrDefault(f => f.NumberFormatId!.Value == id)?.FormatCode?.Value ??
              id.ToString(CultureInfo.InvariantCulture);
    }

    private static bool IsBold(SpreadsheetDocument document, Cell cell)
    {
        Stylesheet stylesheet = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
        CellFormat format = stylesheet.CellFormats!.Elements<CellFormat>().ElementAt((int)Style(cell));
        return stylesheet.Fonts!.Elements<Font>().ElementAt((int)format.FontId!.Value).Bold is not null;
    }

    // title, parameter line, an empty row, then the captions
    [Fact]
    public void Write_HasTheTitleParametersAndCaptions()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("ოთახების ცხრილი", Text(CellAt(worksheet, "A1")));
            Assert.Equal("თარიღისთვის: 02.10.2026", Text(CellAt(worksheet, "A2")));
            Assert.DoesNotContain(worksheet.Descendants<Row>(), r => r.RowIndex!.Value == 3u);
            Assert.Equal(["ოთახი", "დრო", "რაოდენობა", "თანხა", "თარიღი", "დრო და თარიღი"],
                CaptionCells.Select(r => Text(CellAt(worksheet, r))));
            Assert.True(IsBold(document, CellAt(worksheet, "A4")));
            Assert.Equal("r03RoomsAgenda", document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single().Name);
        }
    }

    // numbers, times and dates are Excel numbers with a format, so Excel can sum and sort them
    [Fact]
    public void Write_WritesTheValuesByTheirType()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("R1", Text(CellAt(worksheet, "A5")));
            Cell time = CellAt(worksheet, "B5");
            Assert.Equal(CellValues.Number, time.DataType!.Value);
            Assert.Equal(0.625m, decimal.Parse(time.CellValue!.Text, CultureInfo.InvariantCulture));
            Assert.Equal("hh:mm", NumberFormat(document, time));
            Cell count = CellAt(worksheet, "C5");
            Assert.Equal("2", count.CellValue!.Text);
            Assert.Null(NumberFormat(document, count));
            Cell amount = CellAt(worksheet, "D5");
            Assert.Equal("12.5", amount.CellValue!.Text);
            Assert.Equal("2", NumberFormat(document, amount));
            Cell day = CellAt(worksheet, "E5");
            Assert.Equal(Day.ToOADate(), double.Parse(day.CellValue!.Text, CultureInfo.InvariantCulture));
            Assert.Equal("dd.mm.yyyy", NumberFormat(document, day));
            Cell moment = CellAt(worksheet, "F5");
            Assert.Equal(Day.AddHours(10.5).ToOADate(),
                double.Parse(moment.CellValue!.Text, CultureInfo.InvariantCulture), 9);
            Assert.Equal("dd.mm.yyyy hh:mm", NumberFormat(document, moment));
            Assert.False(IsBold(document, time));
        }
    }

    // an empty cell is not written
    [Fact]
    public void Write_EmptyCells_AreNotWritten()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal(["A6", "C6"],
                worksheet.Descendants<Row>().Single(r => r.RowIndex!.Value == 6u).Elements<Cell>()
                    .Select(c => c.CellReference!.Value!));
        }
    }

    // a section header row, the section's rows and its total; then the report's totals, all totals in bold
    [Fact]
    public void Write_SectionsAndTotals()
    {
        // Arrange
        ReportResponse report = Report(sections:
        [
            new ReportSectionResponse("ჯგუფი: A1", [["R1", null, 1]], ["ჯამი:", null, 1]),
            new ReportSectionResponse("ჯგუფი: B1", [["R2", null, 4]], null)
        ], footerRows: [["სულ:", null, 5], ["საშუალო:", null, 2.5m]]);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("ჯგუფი: A1", Text(CellAt(worksheet, "A5")));
            Assert.True(IsBold(document, CellAt(worksheet, "A5")));
            Assert.Equal("R1", Text(CellAt(worksheet, "A6")));
            Assert.Equal("ჯამი:", Text(CellAt(worksheet, "A7")));
            Assert.True(IsBold(document, CellAt(worksheet, "C7")));
            Assert.Equal("ჯგუფი: B1", Text(CellAt(worksheet, "A8")));
            Assert.Equal("4", CellAt(worksheet, "C9").CellValue!.Text);
            Assert.Equal("სულ:", Text(CellAt(worksheet, "A10")));
            Assert.Equal("5", CellAt(worksheet, "C10").CellValue!.Text);
            Assert.True(IsBold(document, CellAt(worksheet, "C10")));
            Assert.Equal("2", NumberFormat(document, CellAt(worksheet, "C11")));
            //the empty row before the captions is not written
            Assert.Equal(10, worksheet.Descendants<Row>().Count());
        }
    }

    // a section marked NewPage (r01: every group after the first) starts a printed page: a manual row break above
    // its first row across all columns (brk's Id is the previous row), and the breaks are counted; the sheet stays
    // valid
    [Fact]
    public void Write_NewPageSections_BreakThePageAboveThem()
    {
        // Arrange: captions on row 4; A1 on rows 5-6, B1 with its total on rows 7-9, C1 on rows 10-11
        ReportResponse report = Report(sections:
        [
            new ReportSectionResponse("ჯგუფი: A1", [["R1"]], null),
            new ReportSectionResponse("ჯგუფი: B1", [["R2"]], ["ჯამი:"], true),
            new ReportSectionResponse("ჯგუფი: C1", [["R3"]], null, true)
        ]);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("ჯგუფი: B1", Text(CellAt(worksheet, "A7")));
            Assert.Equal("ჯგუფი: C1", Text(CellAt(worksheet, "A10")));
            RowBreaks rowBreaks = worksheet.Elements<RowBreaks>().Single();
            Assert.Equal(2u, rowBreaks.Count!.Value);
            Assert.Equal(2u, rowBreaks.ManualBreakCount!.Value);
            Assert.Equal([(6u, 16383u, true), (9u, 16383u, true)],
                rowBreaks.Elements<Break>().Select(b => (b.Id!.Value, b.Max!.Value, b.ManualPageBreak!.Value)));
            Assert.Empty(new OpenXmlValidator().Validate(document));
        }
    }

    // without NewPage sections the sheet has no page breaks
    [Fact]
    public void Write_NoNewPageSections_HaveNoRowBreaks()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Empty(worksheet.Elements<RowBreaks>());
        }
    }

    // the parameter line and the section headers span all the report's columns (merged), so a long text wraps inside
    // the report's width instead of running past it onto an extra printed sheet; the sheet stays valid
    [Fact]
    public void Write_ParametersAndSectionHeaders_SpanAllTheColumns()
    {
        // Arrange: the parameters on row 2, the captions on row 4, A1 on rows 5-6, B1 on rows 7-8
        ReportResponse report = Report(sections:
        [
            new ReportSectionResponse("ჯგუფი: A1", [["R1"]], null),
            new ReportSectionResponse("ჯგუფი: B1", [["R2"]], null)
        ]);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            MergeCells mergeCells = worksheet.Elements<MergeCells>().Single();
            Assert.Equal(3u, mergeCells.Count!.Value);
            Assert.Equal(["A2:F2", "A5:F5", "A7:F7"],
                mergeCells.Elements<MergeCell>().Select(m => m.Reference!.Value!));
            Assert.Null(RowAt(worksheet, 5).Height);
            Assert.Equal(14u, Style(CellAt(worksheet, "A5")));
            Assert.Equal(14u, Style(CellAt(worksheet, "A7")));
            Assert.Empty(new OpenXmlValidator().Validate(document));
        }
    }

    // nothing is merged without a parameter line and section headers, nor in a one-column report
    [Fact]
    public void Write_NoTextRowsOrOneColumn_MergesNothing()
    {
        // Arrange
        ReportResponse plain = Report() with { Parameters = [] };
        ReportResponse oneColumn = Report(sections: [new ReportSectionResponse("ჯგუფი: A1", [["R1"]], null)],
            footerRows: []) with { Columns = [Columns[0]] };

        // Act
        Worksheet plainSheet = Read(new OpenXmlReportExcelWriter().Write(plain), out SpreadsheetDocument plainDocument);
        Worksheet oneColumnSheet =
            Read(new OpenXmlReportExcelWriter().Write(oneColumn), out SpreadsheetDocument oneColumnDocument);

        // Assert
        using (plainDocument)
        using (oneColumnDocument)
        {
            Assert.Empty(plainSheet.Elements<MergeCells>());
            Assert.Empty(oneColumnSheet.Elements<MergeCells>());
            Assert.Equal("ჯგუფი: A1", Text(CellAt(oneColumnSheet, "A5")));
            Assert.Empty(new OpenXmlValidator().Validate(plainDocument));
        }
    }

    // a parameter line or a header longer than the report's width gets the height of its lines (Excel does not size a
    // merged row itself): its words go on a line while they fit in 90% of the width, a longer word is cut into lines;
    // a short header keeps Excel's own height
    [Fact]
    public void Write_LongTextRows_GetTheHeightOfTheirLines()
    {
        // Arrange: two columns 20 wide (an 18 letter caption and the margin of 2), so a line holds 36: the parameter
        // line "p:" and three words of 9, then 3 and 2 words; the header 3, 3 and 2 words; 120 letters 36, 36, 36, 12
        List<ReportColumnResponse> columns =
        [
            new("a", new string('a', 18), ReportColumnTypes.Text), new("b", new string('b', 18), ReportColumnTypes.Text)
        ];
        string words = string.Join(' ', Enumerable.Repeat("123456789", 8));
        var report = new ReportResponse("rTexts", "Texts",
            [new ReportParameterValueResponse(ReportParameterNames.EndDate, "p", words)], columns,
            [
                new ReportSectionResponse(words, [["x", "y"]], null),
                new ReportSectionResponse("short", [["x", "y"]], null),
                new ReportSectionResponse(new string('z', 120), [["x", "y"]], null)
            ], []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert: the parameters on row 2, the captions on row 4, the headers on rows 5, 7 and 9
        using (document)
        {
            Assert.Equal(45d, RowAt(worksheet, 2).Height!.Value);
            Assert.True(RowAt(worksheet, 2).CustomHeight!.Value);
            Assert.Equal(45d, RowAt(worksheet, 5).Height!.Value);
            Assert.True(RowAt(worksheet, 5).CustomHeight!.Value);
            Assert.Null(RowAt(worksheet, 7).Height);
            Assert.Null(RowAt(worksheet, 7).CustomHeight);
            Assert.Equal(60d, RowAt(worksheet, 9).Height!.Value);
            Assert.Null(RowAt(worksheet, 1).Height);
        }
    }

    // printing: A4, one page wide and as many pages tall as needed (so the NewPage breaks stay), the captions repeated
    // on every printed page (the sheet's Print_Titles)
    [Fact]
    public void Write_PrintsOnA4OnePageWideWithTheCaptionsOnEveryPage()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.True(worksheet.SheetProperties!.PageSetupProperties!.FitToPage!.Value);
            PageSetup pageSetup = worksheet.Elements<PageSetup>().Single();
            Assert.Equal(9u, pageSetup.PaperSize!.Value);
            Assert.Equal(1u, pageSetup.FitToWidth!.Value);
            Assert.Equal(0u, pageSetup.FitToHeight!.Value);
            DefinedName printTitles =
                Assert.Single(document.WorkbookPart!.Workbook!.DefinedNames!.Elements<DefinedName>());
            Assert.Equal("_xlnm.Print_Titles", printTitles.Name!.Value);
            Assert.Equal(0u, printTitles.LocalSheetId!.Value);
            Assert.Equal("'r03RoomsAgenda'!$4:$4", printTitles.Text);
        }
    }

    // a report of more than 12 columns (r30, r36) prints landscape, as on the web page
    [Theory]
    [InlineData(12, false)]
    [InlineData(13, true)]
    public void Write_WideReport_PrintsLandscape(int columnCount, bool landscape)
    {
        // Arrange
        var report = new ReportResponse("rWide", "Wide", [],
        [
            .. Enumerable.Range(1, columnCount)
                .Select(i => new ReportColumnResponse($"c{i}", $"C{i}", ReportColumnTypes.Text))
        ], [], []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal(landscape ? OrientationValues.Landscape : OrientationValues.Portrait,
                worksheet.Elements<PageSetup>().Single().Orientation!.Value);
        }
    }

    // the captions stay on the screen while scrolling
    [Fact]
    public void Write_FreezesTheRowsUpToTheCaptions()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Pane pane = worksheet.Descendants<Pane>().Single();
            Assert.Equal(4d, pane.VerticalSplit!.Value);
            Assert.Equal("A5", pane.TopLeftCell!.Value);
            Assert.Equal(PaneStateValues.Frozen, pane.State!.Value);
            Assert.Equal(0u, worksheet.Descendants<SheetView>().Single().WorkbookViewId!.Value);
        }
    }

    // a report without parameters has no parameter line; its captions print on every page from the third row
    [Fact]
    public void Write_NoParameters_CaptionsOnTheThirdRow()
    {
        // Arrange
        ReportResponse report = Report() with { Parameters = [] };

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("ოთახი", Text(CellAt(worksheet, "A3")));
            Assert.Equal(3d, worksheet.Descendants<Pane>().Single().VerticalSplit!.Value);
            Assert.Equal("'r03RoomsAgenda'!$3:$3",
                document.WorkbookPart!.Workbook!.DefinedNames!.Elements<DefinedName>().Single().Text);
        }
    }

    // a Georgian letter of Sylfaen is 1.3 of Excel's width unit, other characters 1: "ოთახი 12" is 6.5 + 3, and the
    // margin of 2
    [Fact]
    public void Write_GeorgianLettersAreWiderThanTheWidthUnit()
    {
        // Arrange
        ReportResponse report = Report(sections: [new ReportSectionResponse(null, [["ოთახი 12"]], null)],
            footerRows: []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal(11.5, worksheet.Descendants<Column>().First().Width!.Value, 9);
        }
    }

    // widths by the longest text of a column, within limits; the title does not widen the first column;
    // a number with two decimals, a whole number with its digits; the last caption "დრო და თარიღი" (11 Georgian
    // letters) is wider than its date
    [Fact]
    public void Write_ColumnWidthsFollowTheTexts()
    {
        // Arrange
        ReportResponse report = Report(sections:
        [
            new ReportSectionResponse(null,
                [[new string('x', 100), new TimeOnly(9, 0), 123456789012, 1234567.5m, Day, Day]], null)
        ], footerRows: []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            List<Column> columns = [.. worksheet.Descendants<Column>()];
            Assert.Equal([60d, 7d, 14d, 12d, 18d, 18.3d], columns.Select(c => Math.Round(c.Width!.Value, 9)));
            Assert.Equal([1u, 2u, 3u, 4u, 5u, 6u], columns.Select(c => c.Min!.Value));
            Assert.All(columns, c => Assert.Equal(c.Min!.Value, c.Max!.Value));
            Assert.All(columns, c => Assert.True(c.CustomWidth!.Value));
        }
    }

    // a section's total row widens its column too
    [Fact]
    public void Write_SectionTotalWidensItsColumn()
    {
        // Arrange
        ReportResponse report = Report(sections: [new ReportSectionResponse(null, [["R1"]], [new string('y', 30)])],
            footerRows: []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal(32d, worksheet.Descendants<Column>().First().Width!.Value);
        }
    }

    // several parameters on one line, as the page shows them
    [Fact]
    public void Write_SeveralParameters_AreJoinedOnOneLine()
    {
        // Arrange
        ReportResponse report = Report() with
        {
            Parameters =
            [
                new ReportParameterValueResponse(ReportParameterNames.StartDate, "თარიღიდან", "01.09.2026"),
                new ReportParameterValueResponse(ReportParameterNames.EndDate, "თარიღამდე", "02.10.2026")
            ]
        };

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("თარიღიდან: 01.09.2026; თარიღამდე: 02.10.2026", Text(CellAt(worksheet, "A2")));
        }
    }

    // a value beyond the report's columns is still written (a date without a column type is a date) and widened
    [Fact]
    public void Write_RowLongerThanTheColumns_WritesTheExtraValue()
    {
        // Arrange
        ReportResponse report =
            Report(sections: [new ReportSectionResponse(null, [["R1", null, 1, null, null, null, Day]], null)],
                footerRows: []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Cell extra = CellAt(worksheet, "G5");
            Assert.Equal(Day.ToOADate(), double.Parse(extra.CellValue!.Text, CultureInfo.InvariantCulture));
            Assert.Equal("dd.mm.yyyy", NumberFormat(document, extra));
            Assert.Equal(18d, worksheet.Descendants<Column>().Last().Width!.Value);
            Assert.Equal(7, worksheet.Descendants<Column>().Count());
        }
    }

    // a day, other values as their text; a text keeps its spaces
    [Fact]
    public void Write_OtherValues()
    {
        // Arrange
        ReportResponse report =
            Report(
                sections:
                [
                    new ReportSectionResponse(null, [["  R1 ", true, 7L, 1.5d, new DateOnly(2026, 10, 2)]], null)
                ], footerRows: []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Cell text = CellAt(worksheet, "A5");
            Assert.Equal("  R1 ", Text(text));
            Assert.Equal(SpaceProcessingModeValues.Preserve, text.InlineString!.Text!.Space!.Value);
            Assert.Equal("True", Text(CellAt(worksheet, "B5")));
            Assert.Equal(CellValues.InlineString, CellAt(worksheet, "B5").DataType!.Value);
            Assert.Equal("7", CellAt(worksheet, "C5").CellValue!.Text);
            Assert.Null(NumberFormat(document, CellAt(worksheet, "C5")));
            Assert.Equal("1.5", CellAt(worksheet, "D5").CellValue!.Text);
            Assert.Equal("2", NumberFormat(document, CellAt(worksheet, "D5")));
            Cell day = CellAt(worksheet, "E5");
            Assert.Equal(Day.ToOADate(), double.Parse(day.CellValue!.Text, CultureInfo.InvariantCulture));
            Assert.Equal("dd.mm.yyyy", NumberFormat(document, day));
        }
    }

    // fonts: Sylfaen 11 normal and bold, the title bold 14; Excel's reserved fills and an empty border
    [Fact]
    public void Write_StylesheetHasTheFontsFillsAndBorder()
    {
        // Act
        Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Stylesheet stylesheet = document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!;
            Assert.Equal(["Sylfaen 11", "Sylfaen 11 bold", "Sylfaen 14 bold"],
                stylesheet.Fonts!.Elements<Font>().Select(FontText));
            Assert.Equal([PatternValues.None, PatternValues.Gray125],
                stylesheet.Fills!.Elements<Fill>().Select(f => f.PatternFill!.PatternType!.Value));
            Border border = Assert.Single(stylesheet.Borders!.Elements<Border>());
            Assert.NotNull(border.LeftBorder);
            Assert.NotNull(border.RightBorder);
            Assert.NotNull(border.TopBorder);
            Assert.NotNull(border.BottomBorder);
            Assert.NotNull(border.DiagonalBorder);
        }
    }

    // the cell formats: the default (no cell uses it: Excel ignores its alignment), General, Number, Time, Date,
    // DateTime, each normal and bold, the title's, the wrapped text's normal and bold, then the section header's; in a
    // row of several lines the cells stand at its top, the section header at its bottom
    [Fact]
    public void Write_CellFormatsApplyTheirNumberFormatsAndFonts()
    {
        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            List<CellFormat> formats =
                [.. document.WorkbookPart!.WorkbookStylesPart!.Stylesheet!.CellFormats!.Elements<CellFormat>()];
            Assert.Equal([0u, 0u, 0u, 2u, 2u, 164u, 164u, 165u, 165u, 166u, 166u, 0u, 0u, 0u, 0u],
                formats.Select(f => f.NumberFormatId!.Value));
            Assert.Equal(
                [false, false, false, true, true, true, true, true, true, true, true, false, false, false, false],
                formats.Select(f => f.ApplyNumberFormat?.Value ?? false));
            Assert.Equal([0u, 0u, 1u, 0u, 1u, 0u, 1u, 0u, 1u, 0u, 1u, 2u, 0u, 1u, 1u],
                formats.Select(f => f.FontId!.Value));
            Assert.Equal(
                [false, false, true, false, true, false, true, false, true, false, true, true, false, true, true],
                formats.Select(f => f.ApplyFont?.Value ?? false));
            Assert.All(formats, f => Assert.Equal(0u, f.FillId!.Value));
            Assert.All(formats, f => Assert.Equal(0u, f.BorderId!.Value));
            Assert.Equal(
                [false, false, false, false, false, false, false, false, false, false, false, false, true, true, true],
                formats.Select(f => f.Alignment?.WrapText?.Value ?? false));
            Assert.Equal([false, true, true, true, true, true, true, true, true, true, true, false, true, true, false],
                formats.Select(f => f.Alignment?.Vertical?.Value == VerticalAlignmentValues.Top));
            Assert.Equal([false, true, true, true, true, true, true, true, true, true, true, false, true, true, true],
                formats.Select(f => f.ApplyAlignment?.Value ?? false));
            Assert.Equal(11u, Style(CellAt(worksheet, "A1")));
            //the parameters, a caption, a text, a whole number and a time
            Assert.Equal(12u, Style(CellAt(worksheet, "A2")));
            Assert.Equal(13u, Style(CellAt(worksheet, "A4")));
            Assert.Equal(12u, Style(CellAt(worksheet, "A5")));
            Assert.Equal(1u, Style(CellAt(worksheet, "C5")));
            Assert.Equal(5u, Style(CellAt(worksheet, "B5")));
        }
    }

    // Excel opens the file without repairing it: the parts follow the Open XML schema
    [Fact]
    public void Write_IsAValidWorkbook()
    {
        // Act
        Read(new OpenXmlReportExcelWriter().Write(Report()), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Empty(new OpenXmlValidator().Validate(document));
        }
    }

    private static string FontText(Font font)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"{font.FontName!.Val!.Value} {font.FontSize!.Val!.Value}{(font.Bold is null ? "" : " bold")}");
    }

    // Excel's sheet names are at most 31 characters
    [Fact]
    public void Write_LongKey_IsCutToTheSheetNameLimit()
    {
        // Arrange
        string key = new('r', 40);

        // Act
        Read(new OpenXmlReportExcelWriter().Write(Report(key)), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal(new string('r', 31), document.WorkbookPart!.Workbook!.Sheets!.Elements<Sheet>().Single().Name);
        }
    }

    // more than 26 columns: Z is followed by AA
    [Fact]
    public void Write_ManyColumns_NamesThemLikeExcel()
    {
        // Arrange
        List<ReportColumnResponse> columns =
        [
            .. Enumerable.Range(1, 28).Select(i => new ReportColumnResponse($"c{i}", $"C{i}", ReportColumnTypes.Text))
        ];
        var report = new ReportResponse("rMany", "Many", [], columns, [], []);

        // Act
        Worksheet worksheet = Read(new OpenXmlReportExcelWriter().Write(report), out SpreadsheetDocument document);

        // Assert
        using (document)
        {
            Assert.Equal("C26", Text(CellAt(worksheet, "Z3")));
            Assert.Equal("C27", Text(CellAt(worksheet, "AA3")));
            Assert.Equal("C28", Text(CellAt(worksheet, "AB3")));
        }
    }
}
