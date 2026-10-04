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

    // a report without parameters has no parameter line
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
        }
    }

    // widths by the longest text of a column, within limits; the title does not widen the first column;
    // a number with two decimals, a whole number with its digits
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
            Assert.Equal([60d, 7d, 14d, 12d, 18d, 18d], columns.Select(c => c.Width!.Value));
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

    // the cell formats: General, Number, Time, Date, DateTime, each normal and bold, then the title's
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
            Assert.Equal([0u, 0u, 2u, 2u, 164u, 164u, 165u, 165u, 166u, 166u, 0u],
                formats.Select(f => f.NumberFormatId!.Value));
            Assert.Equal([false, false, true, true, true, true, true, true, true, true, false],
                formats.Select(f => f.ApplyNumberFormat?.Value ?? false));
            Assert.Equal([0u, 1u, 0u, 1u, 0u, 1u, 0u, 1u, 0u, 1u, 2u], formats.Select(f => f.FontId!.Value));
            Assert.Equal([false, true, false, true, false, true, false, true, false, true, true],
                formats.Select(f => f.ApplyFont?.Value ?? false));
            Assert.All(formats, f => Assert.Equal(0u, f.FillId!.Value));
            Assert.All(formats, f => Assert.Equal(0u, f.BorderId!.Value));
            Assert.Equal(10u, Style(CellAt(worksheet, "A1")));
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
