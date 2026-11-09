using System;
using System.Collections.Generic;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGe.Application.BankTransactions.Models;
using Xunit;

namespace AppMimosiGe.Application.Tests.BankTransactions;

public sealed class PaymentMatcherTests
{
    //მიმდინარე წლის მოსწავლეები (სინთეზური სახელები): 1 და 2 და-ძმაა (გადამხდელი 100), 3 სხვა ოჯახია იგივე გვარით
    //(გადამხდელი 101), 4 სხვა გვარისაა (გადამხდელი 102); 9 მხოლოდ წინა წლის მოსწავლეა
    private static readonly PaymentMatchInput Input = new([
            new MatchStudent(1, "ნინო", "ბერიძე"),
            new MatchStudent(2, "გიორგი", "ბერიძე"),
            new MatchStudent(3, "ანა", "ბერიძე"),
            new MatchStudent(4, "ლუკა", "გელაშვილი")
        ],
        new Dictionary<string, int> { ["6.001"] = 1, ["6.004"] = 4, ["5.090"] = 2, ["5.091"] = 9 },
        [
            new MatchPayer(100, "01001000100", "თამარ", "ბერიძე", [1, 2]),
            new MatchPayer(101, "01001000101", "დავით", "ბერიძე", [3]),
            new MatchPayer(102, "01001000102", "ეკა", "კაპანაძე", [4])
        ],
        [
            new LearnedPayer(4, "01001000200", null),
            new LearnedPayer(3, null, "GE00TB0000000000000300"),
            new LearnedPayer(9, "01001000999", "GE00TB0000000000000999")
        ]);

    private static BankMovement Movement(string? purpose = null, string? partnerName = null,
        string? partnerTaxCode = null, string? partnerPersonalNumber = null, string? partnerIban = null,
        string? additionalInformation = null, string? additionalDescription = null)
    {
        return new BankMovement("1.2", new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Unspecified), null, 100m, "GEL", purpose, additionalInformation,
            additionalDescription, null, partnerName, partnerIban, "TBCBGE22", partnerTaxCode, partnerPersonalNumber);
    }

    private static void AssertMatch(EPaymentMatchStep step, int[] students, BankMovement movement)
    {
        PaymentMatch match = PaymentMatcher.Match(movement, Input);
        Assert.Equal(step, match.Step);
        Assert.Equal(students, match.StudentHumanIds);
    }

    [Fact]
    public void Match_ContractNumberComesFirst()
    {
        // the text names another student too, but the contract number wins
        AssertMatch(EPaymentMatchStep.ContractNumber, [4],
            Movement("ხელშ. 6.004 გიორგი ბერიძე", "თამარ ბერიძე", "01001000100"));
    }

    [Fact]
    public void Match_ContractNumberOfAPreviousYear_IsTheSameStudent()
    {
        AssertMatch(EPaymentMatchStep.ContractNumber, [2], Movement("5-090"));
    }

    [Fact]
    public void Match_SeveralContractNumbers_AreSeveralStudents()
    {
        AssertMatch(EPaymentMatchStep.ContractNumber, [1, 4], Movement("6.004, 6.001"));
    }

    [Fact]
    public void Match_ContractNumberOfAStudentWithoutACurrentContractOrUnknown_IsSkipped()
    {
        AssertMatch(EPaymentMatchStep.StudentName, [1], Movement("5.091 6.777 ნინო ბერიძისთვის"));
    }

    [Fact]
    public void Match_FirstNameAndSurname_IsThatStudent()
    {
        AssertMatch(EPaymentMatchStep.StudentName, [2], Movement("გიორგი ბერიძის სწავლის საფასური"));
    }

    [Fact]
    public void Match_TwoFirstNamesWithTheSurname_AreBothStudents()
    {
        AssertMatch(EPaymentMatchStep.StudentName, [1, 2], Movement("ნინოსა და გიორგი ბერიძეებისთვის"));
    }

    [Fact]
    public void Match_SurnameOnly_IsEveryStudentWithIt()
    {
        AssertMatch(EPaymentMatchStep.StudentSurname, [1, 2, 3], Movement("BERIDZE"));
    }

    [Fact]
    public void Match_LooksInTheAdditionalDescriptionToo()
    {
        AssertMatch(EPaymentMatchStep.StudentName, [4],
            Movement("გადარიცხვა", additionalDescription: "ლუკა გელაშვილი"));
    }

    [Fact]
    public void Match_ThePayerNameInTheAdditionalInformationIsNotAStudentName()
    {
        // the bank writes the payer into additionalInformation: her surname must not count as the students' surname,
        // so her personal number finds her own children
        AssertMatch(EPaymentMatchStep.PayerPersonalNumber, [1, 2],
            Movement("სწავლა", "თამარ ბერიძე", "01001000100",
                additionalInformation: "თამარ ბერიძე TBCBGE22 GE00TB0000000000000100 სწავლა"));
    }

    [Fact]
    public void Match_ThePayerNameInTheAdditionalInformationIsSkippedWordByWord()
    {
        // TBC's real format: partnerName ends with ", <personal number>" and additionalInformation may name the payer
        // surname first
        AssertMatch(EPaymentMatchStep.PayerPersonalNumber, [1, 2],
            Movement("სწავლა", "თამარ ბერიძე, 01001000100", "01001000100",
                additionalInformation: "ბერიძე თამარ TBCBGE22 GE00TB0000000000000100   "));
    }

    [Fact]
    public void Match_StudentNameInTheAdditionalInformationCounts()
    {
        AssertMatch(EPaymentMatchStep.StudentName, [4],
            Movement("სწავლა", "ეკა კაპანაძე", additionalInformation: "Eka Kapanadze luka gelashvili"));
    }

    [Fact]
    public void Match_ContractPayerPersonalNumberInTheTaxCode_IsAllHerStudents()
    {
        AssertMatch(EPaymentMatchStep.PayerPersonalNumber, [1, 2], Movement("სწავლა", partnerTaxCode: "01001000100"));
    }

    [Fact]
    public void Match_PersonalNumberOfACashOperation_IsUsedWhenTheTaxCodeIsNotOne()
    {
        AssertMatch(EPaymentMatchStep.PayerPersonalNumber, [4],
            Movement(partnerTaxCode: "204567891", partnerPersonalNumber: " 01001000200 "));
    }

    [Fact]
    public void Match_LearnedPayerPersonalNumber_IsHisStudent()
    {
        AssertMatch(EPaymentMatchStep.PayerPersonalNumber, [4], Movement(partnerTaxCode: "01001000200"));
    }

    [Fact]
    public void Match_LearnedPayerIban_IsHisStudent()
    {
        AssertMatch(EPaymentMatchStep.PayerAccount, [3], Movement(partnerIban: "ge00 tb00 0000 0000 0003 00"));
    }

    [Fact]
    public void Match_UnknownPersonalNumber_TriesTheIbanButNotTheName()
    {
        AssertMatch(EPaymentMatchStep.PayerAccount, [3],
            Movement(partnerName: "ეკა კაპანაძე", partnerTaxCode: "01001000555", partnerIban: "GE00TB0000000000000300"));
        // a namesake of the payer: her surname is not a student's, so nothing is found
        AssertMatch(EPaymentMatchStep.None, [], Movement(partnerName: "ეკა კაპანაძე", partnerTaxCode: "01001000555"));
    }

    [Fact]
    public void Match_LearnedPayerOfAStudentWithoutACurrentContract_IsSkipped()
    {
        AssertMatch(EPaymentMatchStep.None, [],
            Movement(partnerTaxCode: "01001000999", partnerIban: "GE00TB0000000000000999"));
    }

    [Fact]
    public void Match_PayerNameWithoutPersonalNumber_IsHerStudents()
    {
        AssertMatch(EPaymentMatchStep.PayerName, [4], Movement("სწავლა", "KAPANADZE EKA"));
    }

    [Fact]
    public void Match_PayerNameOfSeveralPayers_IsNotUsed()
    {
        // two different payers with the same name: the surname rule finds nobody either
        var input = Input with
        {
            Payers = [.. Input.Payers, new MatchPayer(103, "01001000103", "ეკა", "კაპანაძე", [1])]
        };

        PaymentMatch match = PaymentMatcher.Match(Movement(partnerName: "ეკა კაპანაძე"), input);

        Assert.Equal(EPaymentMatchStep.None, match.Step);
    }

    [Fact]
    public void Match_PayerSurnameEqualToTheStudents_IsThoseStudents()
    {
        AssertMatch(EPaymentMatchStep.PayerSurname, [4], Movement("სწავლა", "მარინე გელაშვილი"));
        AssertMatch(EPaymentMatchStep.PayerSurname, [1, 2, 3], Movement("სწავლა", "ლია ბერიძე", "01001000777"));
    }

    [Fact]
    public void Match_NothingFound_IsNone()
    {
        AssertMatch(EPaymentMatchStep.None, [], Movement("სწავლის საფასური", "შპს კომპანია", "204567891"));
        AssertMatch(EPaymentMatchStep.None, [], Movement());
    }
}
