using System.Collections.Generic;
using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class PaymentMatchInputFactoryTests
{
    //student 2 has two current contracts (old data): the later one counts; payer 100 pays for 1 and 2
    private static readonly MatchContractData[] Contracts =
    [
        new(12, 2, "გიორგი", "ბერიძე", 100, "01001000100", "თამარ", "ბერიძე"),
        new(11, 1, "ნინო", "ბერიძე", 100, "01001000100", "თამარ", "ბერიძე"),
        new(15, 2, "გიორგი", "ბერიძე", 100, "01001000100", "თამარ", "ბერიძე"),
        new(13, 3, "ლუკა", "გელაშვილი", 101, "01001000101", "ეკა", "კაპანაძე")
    ];

    [Fact]
    public void Create_StudentsAndPayersOnceEach()
    {
        Dictionary<string, int> byNumber = new() { ["6.001"] = 1 };
        LearnedPayer[] learned = [new(3, "01001000200", null)];

        PaymentMatchInput input = PaymentMatchInputFactory.Create(Contracts, byNumber, learned);

        Assert.Equal([
            new MatchStudent(1, "ნინო", "ბერიძე"), new MatchStudent(2, "გიორგი", "ბერიძე"),
            new MatchStudent(3, "ლუკა", "გელაშვილი")
        ], input.Students);
        Assert.Equal(2, input.Payers.Count);
        Assert.Equal((100, "01001000100", "თამარ", "ბერიძე"), (input.Payers[0].PayerHumanId,
            input.Payers[0].PersonalId, input.Payers[0].FirstName, input.Payers[0].LastName));
        Assert.Equal([1, 2], input.Payers[0].StudentHumanIds);
        Assert.Equal([3], input.Payers[1].StudentHumanIds);
        Assert.Same(byNumber, input.StudentByContractNumber);
        Assert.Same(learned, input.LearnedPayers);
    }

    [Fact]
    public void CurrentContractByStudent_IsTheLatestContract()
    {
        Assert.Equal(new Dictionary<int, int> { [1] = 11, [2] = 15, [3] = 13 },
            PaymentMatchInputFactory.CurrentContractByStudent(Contracts));
    }
}
