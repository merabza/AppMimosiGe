using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.BankTransactions;

/// <summary>
///     ბანკის ანგარიშის ისტორიის წამკითხველი (D166: თიბისი ახლა, საქართველოს ბანკი მერე). მხოლოდ კითხულობს, ბანკში
///     არაფერს ცვლის
/// </summary>
public interface IBankMovementsClient
{
    /// <summary>
    ///     ინტეგრირებული ანგარიში და გადასვლის დღე; null, თუ კონფიგურაციაში შევსებული არ არის
    /// </summary>
    IntegratedBankAccount? Account { get; }

    /// <summary>
    ///     ინტეგრირებულ ანგარიშზე შემოსული, ავტორიზებული ჩარიცხვები პერიოდში (ორივე თარიღი ჩათვლით), ბანკის რიგით
    /// </summary>
    Task<Result<IReadOnlyList<BankMovement>>> GetIncomingPayments(DateOnly periodFrom, DateOnly periodTo,
        CancellationToken cancellationToken);
}
