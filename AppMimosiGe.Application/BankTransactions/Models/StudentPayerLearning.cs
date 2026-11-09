using System.Collections.Generic;
using System.Linq;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     გადამხდელის მიბმა მოსწავლეებზე ხელით განაწილებისას (D169): ჩარიცხვის გადამხდელი ებმება ყველა მოსწავლეს, ვისზეც
///     თანხა გაანაწილეს, თუ ჩარიცხვას პირადი ნომერი ან IBAN აქვს და მოსწავლეს ეს გადამხდელი უკვე არ ჰყავს (პირადი ნომრით,
///     მის გარეშე IBAN-ით). ბრუნდება მხოლოდ ახალი მიბმები
/// </summary>
public static class StudentPayerLearning
{
    public static List<StudentPayer> NewLinks(BankTransaction bankTransaction, IEnumerable<int> studentHumanIds,
        IReadOnlyCollection<StudentPayer> existing)
    {
        string? personalNumber =
            PayerIdentity.PersonalNumber(bankTransaction.PartnerTaxCode, bankTransaction.PartnerPersonalNumber);
        string? iban = PayerIdentity.Iban(bankTransaction.PartnerAccountIban);
        if (personalNumber is null && iban is null)
        {
            return [];
        }

        return
        [
            .. studentHumanIds.Distinct().Order()
                .Where(student => !existing.Any(p => p.StudentHumanId == student && (personalNumber is not null
                    ? p.PersonalNumber == personalNumber
                    : PayerIdentity.Iban(p.AccountIban) == iban)))
                .Select(student => new StudentPayer
                {
                    StudentHumanId = student,
                    PersonalNumber = personalNumber,
                    AccountIban = iban,
                    PayerName = bankTransaction.PartnerName
                })
        ];
    }
}
