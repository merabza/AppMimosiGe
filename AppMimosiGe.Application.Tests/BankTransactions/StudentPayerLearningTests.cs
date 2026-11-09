using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.BankTransactions.Models;
using MimosiGeCore.Domain.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class StudentPayerLearningTests
{
    private static BankTransaction Transaction(string? taxCode = "01001000100", string? personalNumber = null,
        string? iban = "ge00 tb00 0000 0000 0001 00")
    {
        return new BankTransaction
        {
            MovementId = "1",
            Currency = "GEL",
            PartnerName = "თამარ ბერიძე",
            PartnerTaxCode = taxCode,
            PartnerPersonalNumber = personalNumber,
            PartnerAccountIban = iban
        };
    }

    private static (int, string?, string?, string?)[] Links(IEnumerable<StudentPayer> links)
    {
        return [.. links.Select(l => (l.StudentHumanId, l.PersonalNumber, l.AccountIban, l.PayerName))];
    }

    [Fact]
    public void NewLinks_ThePayerGoesToEveryStudentOnce()
    {
        Assert.Equal([
            (1, "01001000100", "GE00TB0000000000000100", "თამარ ბერიძე"),
            (2, "01001000100", "GE00TB0000000000000100", "თამარ ბერიძე")
        ], Links(StudentPayerLearning.NewLinks(Transaction(), [2, 1, 2], [])));
    }

    [Fact]
    public void NewLinks_KnownPayerOfAStudent_IsSkippedByPersonalNumber()
    {
        StudentPayer known = new() { StudentHumanId = 1, PersonalNumber = "01001000100", AccountIban = "OTHER" };
        StudentPayer otherStudent = new() { StudentHumanId = 3, PersonalNumber = "01001000100" };

        Assert.Equal([2], StudentPayerLearning.NewLinks(Transaction(), [1, 2], [known, otherStudent])
            .Select(l => l.StudentHumanId));
    }

    [Fact]
    public void NewLinks_WithoutAPersonalNumber_TheIbanIdentifies()
    {
        StudentPayer known = new() { StudentHumanId = 1, AccountIban = "GE00TB0000000000000100" };

        List<StudentPayer> links =
            StudentPayerLearning.NewLinks(Transaction(taxCode: "204567891"), [1, 2], [known]);

        Assert.Equal([(2, null, "GE00TB0000000000000100", "თამარ ბერიძე")], Links(links));
    }

    [Fact]
    public void NewLinks_PersonalNumberOfACashOperation_Counts()
    {
        Assert.Equal("01001000200",
            Assert.Single(StudentPayerLearning.NewLinks(Transaction(null, "01001000200", null), [1], []))
                .PersonalNumber);
    }

    [Fact]
    public void NewLinks_NoIdentifier_LinksNothing()
    {
        Assert.Empty(StudentPayerLearning.NewLinks(Transaction("204567891", null, " "), [1], []));
    }
}
