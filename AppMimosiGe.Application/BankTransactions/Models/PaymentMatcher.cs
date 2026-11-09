using System;
using System.Collections.Generic;
using System.Linq;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ვის ეკუთვნის ჩარიცხვა (TBC_INTEGRATION.md §4, D162, D165). წესები რიგით, პირველივე, რომელიც ვინმეს პოულობს:
///     0. კონტრაქტის ნომერი დანიშნულებაში; 1. მოსწავლის სახელი და გვარი, ან მხოლოდ გვარი დანიშნულებაში; 2. გადამხდელი
///     ჩვენს ბაზაში (პირადი ნომრით, IBAN-ით, პირადი ნომრის გარეშე სახელითა და გვარით); 3. გადამხდელის გვარი = მოსწავლის
///     გვარი. კანდიდატები მხოლოდ მიმდინარე წლის მოსწავლეები არიან. additionalInformation-ში ბანკი გადამხდელის სახელს
///     წერს (ზოგჯერ სხვა რიგით, partnerName-ში კი ბოლოში ", პირადი ნომერი" ერთვის), ამიტომ იქიდან გადამხდელის სახელის
///     სიტყვები არ ითვლება, რომ გადამხდელის გვარი მოსწავლის გვარად არ ჩაითვალოს და მე-2 წესამდე მივიდეთ
/// </summary>
public static class PaymentMatcher
{
    public static PaymentMatch Match(BankMovement movement, PaymentMatchInput input)
    {
        HashSet<int> current = [.. input.Students.Select(s => s.StudentHumanId)];
        string text = string.Join(' ', movement.Purpose, movement.AdditionalDescription, movement.AdditionalInformation);
        HashSet<string> partnerTokens = [.. LatinSkeleton.Tokens(movement.PartnerName)];
        List<string> tokens =
        [
            .. LatinSkeleton.Tokens(movement.Purpose), .. LatinSkeleton.Tokens(movement.AdditionalDescription),
            .. LatinSkeleton.Tokens(movement.AdditionalInformation).Where(token => !partnerTokens.Contains(token))
        ];

        return ByContractNumber(text, input, current) ?? ByStudentName(tokens, input) ?? ByPayer(movement, input, current) ??
            ByPayerSurname(movement, input) ?? new PaymentMatch(EPaymentMatchStep.None, []);
    }

    private static PaymentMatch? ByContractNumber(string text, PaymentMatchInput input, HashSet<int> current)
    {
        return Found(EPaymentMatchStep.ContractNumber,
            ContractNumberFinder.Find(text)
                .Select(number => input.StudentByContractNumber.TryGetValue(number, out int student) ? student : 0)
                .Where(current.Contains));
    }

    private static PaymentMatch? ByStudentName(List<string> tokens, PaymentMatchInput input)
    {
        List<MatchStudent> bySurname = [.. input.Students.Where(s => NameMatcher.ContainsName(tokens, s.LastName))];
        List<MatchStudent> byName = [.. bySurname.Where(s => NameMatcher.ContainsName(tokens, s.FirstName))];
        return byName.Count > 0
            ? Found(EPaymentMatchStep.StudentName, byName.Select(s => s.StudentHumanId))
            : Found(EPaymentMatchStep.StudentSurname, bySurname.Select(s => s.StudentHumanId));
    }

    private static PaymentMatch? ByPayer(BankMovement movement, PaymentMatchInput input, HashSet<int> current)
    {
        string? personalId = PayerIdentity.PersonalNumber(movement.PartnerTaxCode, movement.PartnerPersonalNumber);
        if (personalId is not null)
        {
            PaymentMatch? byPersonalId = Found(EPaymentMatchStep.PayerPersonalNumber,
                input.Payers.Where(p => p.PersonalId == personalId).SelectMany(p => p.StudentHumanIds)
                    .Concat(input.LearnedPayers.Where(l => l.PersonalNumber == personalId)
                        .Select(l => l.StudentHumanId)).Where(current.Contains));
            if (byPersonalId is not null)
            {
                return byPersonalId;
            }
        }

        string? iban = PayerIdentity.Iban(movement.PartnerIban);
        PaymentMatch? byIban = iban is null
            ? null
            : Found(EPaymentMatchStep.PayerAccount,
                input.LearnedPayers.Where(l => PayerIdentity.Iban(l.AccountIban) == iban).Select(l => l.StudentHumanId)
                    .Where(current.Contains));
        if (byIban is not null || personalId is not null)
        {
            //პირადი ნომრით უცნობი გადამხდელი სახელით აღარ ვეძებთ (სხვა ადამიანია, მოსახელე)
            return byIban;
        }

        List<string> partnerTokens = LatinSkeleton.Tokens(movement.PartnerName);
        List<MatchPayer> byName =
        [
            .. input.Payers.Where(p => NameMatcher.ContainsExactName(partnerTokens, p.FirstName) &&
                                       NameMatcher.ContainsExactName(partnerTokens, p.LastName))
        ];
        return byName.Select(p => p.PayerHumanId).Distinct().Count() == 1
            ? Found(EPaymentMatchStep.PayerName, byName.SelectMany(p => p.StudentHumanIds).Where(current.Contains))
            : null;
    }

    private static PaymentMatch? ByPayerSurname(BankMovement movement, PaymentMatchInput input)
    {
        List<string> partnerTokens = LatinSkeleton.Tokens(movement.PartnerName);
        return Found(EPaymentMatchStep.PayerSurname,
            input.Students.Where(s => NameMatcher.ContainsName(partnerTokens, s.LastName))
                .Select(s => s.StudentHumanId));
    }

    private static PaymentMatch? Found(EPaymentMatchStep step, IEnumerable<int> studentHumanIds)
    {
        List<int> students = [.. studentHumanIds.Distinct().Order()];
        return students.Count == 0 ? null : new PaymentMatch(step, students);
    }
}
