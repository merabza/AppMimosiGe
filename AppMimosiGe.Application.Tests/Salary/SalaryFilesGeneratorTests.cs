using System;
using System.Text;
using AppMimosiGe.Application.Salary.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.Salary;

public sealed class SalaryFilesGeneratorTests
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private static TransferFileRow Transfer(string? bankAccount, decimal amount, string firstName = "ანა",
        string lastName = "ბერიძე", string? legalName = null, string? description = null,
        string? quoteTypeName = "ხელფასი", string personalId = "01001012345", string monthName = "სექტემბერი",
        int year = 2026)
    {
        return new TransferFileRow(bankAccount, legalName, firstName, lastName, personalId, amount, description,
            quoteTypeName, monthName, year);
    }

    private static DeclarationFileRow Declaration(int saId, string personalId = "1001012345",
        string? legalAddress = "თბილისი, ვაკე", int? rsQuoteTypeId = 1, decimal gross = 4772.5m,
        string? legalName = null)
    {
        return new DeclarationFileRow(saId, personalId, legalName, "ანა", "ბერიძე", legalAddress, "268", rsQuoteTypeId,
            gross, new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified));
    }

    private static string Text(byte[] content)
    {
        Assert.Equal(Bom, content[..3]);
        return Encoding.UTF8.GetString(content, 3, content.Length - 3);
    }

    // --- გადარიცხვის ფაილი ---

    [Fact]
    public void TransferFile_IsUtf8WithBomCrLfAndNoTrailingNewLine()
    {
        // Act
        byte[] content = SalaryFilesGenerator.TransferFile([Transfer("GE01TB0000000000000001", 3771.44m)]);

        // Assert
        Assert.Equal(
            "DOCNUM,ACCIBANTO,BENEFNAME,BENEFTAXCODE,AMOUNT,DESCR,ADDDESCR\r\n" +
            " 1,GE01TB0000000000000001,ანა ბერიძე,\"01001012345\",3771.44,ხელფასი,სექტემბერი  2026", Text(content));
        //ქართული ასო UTF-8-ში სამი ბაიტია ("ანა" = E1 83 90, E1 83 9C, E1 83 90)
        Assert.Contains("E18390E1839CE18390", Convert.ToHexString(content), StringComparison.Ordinal);
    }

    [Fact]
    public void TransferFile_WithoutRows_IsTheHeaderOnly()
    {
        Assert.Equal(SalaryFilesGenerator.TransferFileHeader, Text(SalaryFilesGenerator.TransferFile([])));
    }

    [Fact]
    public void TransferFile_RowsAreInAccessGroupByOrderAndNumberedWithVbaStr()
    {
        // Act: ანგარიშის გარეშე პირველია, შემდეგ ანგარიშით; ერთნაირი სტრიქონები აღარ ერთიანდება
        string text = Text(SalaryFilesGenerator.TransferFile(
        [
            Transfer("GE02", 10m), Transfer("GE01", 20m), Transfer(null, 30m), Transfer("GE02", 10m),
            Transfer("GE01", 5m)
        ]));

        // Assert
        Assert.Equal(
        [
            SalaryFilesGenerator.TransferFileHeader, " 1,,ანა ბერიძე,\"01001012345\",30,ხელფასი,სექტემბერი  2026",
            " 2,GE01,ანა ბერიძე,\"01001012345\",5,ხელფასი,სექტემბერი  2026",
            " 3,GE01,ანა ბერიძე,\"01001012345\",20,ხელფასი,სექტემბერი  2026",
            " 4,GE02,ანა ბერიძე,\"01001012345\",10,ხელფასი,სექტემბერი  2026",
            " 5,GE02,ანა ბერიძე,\"01001012345\",10,ხელფასი,სექტემბერი  2026"
        ], text.Split("\r\n"));
    }

    [Fact]
    public void TransferFile_SameAccountIsOrderedByNameThenPersonalId()
    {
        // Act
        string[] lines = Text(SalaryFilesGenerator.TransferFile(
        [
            Transfer("GE01", 1m, "ბ"), Transfer("GE01", 1m, "ა", personalId: "2"),
            Transfer("GE01", 1m, "ა", personalId: "1")
        ])).Split("\r\n");

        // Assert
        Assert.StartsWith(" 1,GE01,ა ბერიძე,\"1\"", lines[1], StringComparison.Ordinal);
        Assert.StartsWith(" 2,GE01,ა ბერიძე,\"2\"", lines[2], StringComparison.Ordinal);
        Assert.StartsWith(" 3,GE01,ბ ბერიძე", lines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void TransferFile_LegalNameReplacesTheFirstNameAndDescriptionReplacesTheQuoteType()
    {
        // Act
        string line = Text(SalaryFilesGenerator.TransferFile([
            Transfer("GE01", 800m, legalName: "შპს ალფა", description: "დივიდენდი")
        ])).Split("\r\n")[1];

        // Assert
        Assert.Equal(" 1,GE01,შპს ალფა ბერიძე,\"01001012345\",800,დივიდენდი,სექტემბერი  2026", line);
    }

    [Fact]
    public void TransferFile_WithoutDescriptionAndQuoteType_HasAnEmptyDescription()
    {
        // Act
        string line = Text(SalaryFilesGenerator.TransferFile([Transfer("GE01", 1.5m, quoteTypeName: null)]))
            .Split("\r\n")[1];

        // Assert
        Assert.Equal(" 1,GE01,ანა ბერიძე,\"01001012345\",1.5,,სექტემბერი  2026", line);
    }

    // two contracts of one person with the same account and amount: the description, then the month decide
    [Fact]
    public void TransferFile_EqualRowsAreOrderedByDescriptionThenMonth()
    {
        // Act
        string[] lines = Text(SalaryFilesGenerator.TransferFile(
        [
            Transfer("GE01", 1m, description: "ბ"), Transfer("GE01", 1m, description: "ა", monthName: "ოქტომბერი"),
            Transfer("GE01", 1m, description: "ა", monthName: "აგვისტო", year: 2027),
            Transfer("GE01", 1m, description: "ა", monthName: "აგვისტო")
        ])).Split("\r\n");

        // Assert
        Assert.Equal(
        [
            " 1,GE01,ანა ბერიძე,\"01001012345\",1,ა,აგვისტო  2026",
            " 2,GE01,ანა ბერიძე,\"01001012345\",1,ა,აგვისტო  2027",
            " 3,GE01,ანა ბერიძე,\"01001012345\",1,ა,ოქტომბერი  2026",
            " 4,GE01,ანა ბერიძე,\"01001012345\",1,ბ,სექტემბერი  2026"
        ], lines[1..]);
    }

    // money columns come from SQL as decimals with four decimal places (money)
    [Fact]
    public void TransferFile_DatabaseAmountsLoseTheirTrailingZeros()
    {
        // Act
        string line = Text(SalaryFilesGenerator.TransferFile([Transfer("GE01", 862.5000m)])).Split("\r\n")[1];

        // Assert
        Assert.Equal(" 1,GE01,ანა ბერიძე,\"01001012345\",862.5,ხელფასი,სექტემბერი  2026", line);
    }

    [Theory]
    [InlineData(2026, 10, 5, "salary_2026_10_5.csv")]
    [InlineData(2026, 1, 15, "salary_2026_1_15.csv")]
    public void TransferFileName_HasNoLeadingZeros(int year, int month, int day, string expected)
    {
        Assert.Equal(expected,
            SalaryFilesGenerator.TransferFileName(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    // --- დეკლარაციის ფაილი ---

    [Fact]
    public void DeclarationFile_IsTheAccessFormat()
    {
        // Act
        string text = Text(SalaryFilesGenerator.DeclarationFile([Declaration(1)]));

        // Assert: პირადი ნომერი 11 ციფრით და ბრჭყალებში, მძიმიანი მისამართი ბრჭყალებში
        Assert.Equal(SalaryFilesGenerator.DeclarationFileHeader + "\r\n" +
                     "\"01001012345\",ანა,ბერიძე,\"თბილისი, ვაკე\",268,4,1,4772.5,0,05.10.2026,20,0,0", text);
    }

    [Fact]
    public void DeclarationFile_RowsAreOrderedByLineAndEmptyValuesAreEmptyFields()
    {
        // Act
        string[] lines = Text(SalaryFilesGenerator.DeclarationFile(
        [
            Declaration(2, legalAddress: null, rsQuoteTypeId: null, gross: 1250m, legalName: "შპს, ალფა"),
            Declaration(1, gross: 0.1m)
        ])).Split("\r\n");

        // Assert
        Assert.Equal(3, lines.Length);
        Assert.Equal("\"01001012345\",ანა,ბერიძე,\"თბილისი, ვაკე\",268,4,1,0.1,0,05.10.2026,20,0,0", lines[1]);
        Assert.Equal("\"01001012345\",\"შპს, ალფა\",ბერიძე,,268,4,,1250,0,05.10.2026,20,0,0", lines[2]);
    }

    [Fact]
    public void DeclarationFileHeader_IsTheGeoPhraseFour()
    {
        Assert.StartsWith("\"საიდენტიფიკაციო ნომერი (პირადი ნომერი)\",\"თანხის მიმღების სახელი",
            SalaryFilesGenerator.DeclarationFileHeader, StringComparison.Ordinal);
        Assert.EndsWith("ჩათვლას დაქვემდებარებული, უცხო ქვეყანაში გადახდილი გადასახადის თანხა\"",
            SalaryFilesGenerator.DeclarationFileHeader, StringComparison.Ordinal);
        Assert.Equal(13, SalaryFilesGenerator.DeclarationFileHeader.Split("\",\"").Length);
    }

    [Theory]
    [InlineData(2026, 10, "TaxDepDeclaration_2026_10.csv")]
    [InlineData(2027, 3, "TaxDepDeclaration_2027_3.csv")]
    public void DeclarationFileName_HasNoLeadingZeros(int year, int month, string expected)
    {
        Assert.Equal(expected,
            SalaryFilesGenerator.DeclarationFileName(new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified)));
    }

    // --- VBA-ს გარდაქმნები ---

    [Theory]
    [InlineData("abc", false, "abc")]
    [InlineData("a,b", false, "\"a,b\"")]
    [InlineData("abc", true, "\"abc\"")]
    [InlineData("", false, "")]
    [InlineData("a\"b", false, "a\"b")]
    public void CareCsv_QuotesOnlyTextWithACommaOrWhenForced(string value, bool force, string expected)
    {
        Assert.Equal(expected, SalaryFilesGenerator.CareCsv(value, force));
    }

    [Theory]
    [InlineData(1, " 1")]
    [InlineData(0, " 0")]
    [InlineData(2026, " 2026")]
    [InlineData(-3, "-3")]
    public void VbaStr_AddsALeadingSpaceToNonNegativeNumbers(int value, string expected)
    {
        Assert.Equal(expected, SalaryFilesGenerator.VbaStr(value));
    }

    [Theory]
    [InlineData(3771.44, "3771.44")]
    [InlineData(800, "800")]
    [InlineData(862.5, "862.5")]
    [InlineData(0.1234, "0.1234")]
    [InlineData(-12.3, "-12.3")]
    [InlineData(0, "0")]
    public void VbaNumber_IsTheCurrencyTextWithoutTrailingZeros(decimal value, string expected)
    {
        Assert.Equal(expected, SalaryFilesGenerator.VbaNumber(value));
    }

    [Fact]
    public void VbaNumber_DatabaseScaleDoesNotChangeTheText()
    {
        Assert.Equal(("800", "3771.44", "0.0001"),
            (SalaryFilesGenerator.VbaNumber(800.0000m), SalaryFilesGenerator.VbaNumber(3771.4400m),
                SalaryFilesGenerator.VbaNumber(0.0001m)));
    }

    [Theory]
    [InlineData("01001012345", "01001012345")]
    [InlineData("1001012345", "01001012345")]
    [InlineData("000123", "00000000123")]
    [InlineData("012345678901", "12345678901")]
    [InlineData("A1234", "A1234")]
    [InlineData("", "")]
    public void FormatPersonalId_PadsDigitsToElevenAsVbaFormat(string personalId, string expected)
    {
        Assert.Equal(expected, SalaryFilesGenerator.FormatPersonalId(personalId));
    }
}
