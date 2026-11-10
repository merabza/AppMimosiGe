using System.Collections.Generic;
using System.Linq;
using AppMimosiGe.Application.BankTransactions;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Payments.Validation;

/// <summary>
///     გადახდის ველების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება. Checked-ის უფლებას handler-ი ამოწმებს.
///     Access-ში არ იყო, მომხმარებლის არჩევანით დაემატა: გადახდის სახე სავალდებულოა და თანხა 0 ვერ იქნება
/// </summary>
public sealed class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    //დოკუმენტის სვეტის სიგრძე (Access-ისაც)
    public const int DocumentMaxLength = 255;

    //თანხის ათწილადები: Access-ის მონაცემებში 2-ზე მეტი არ არის
    public const int AmountDecimals = 2;

    //bankClients: ბანკების კლიენტები, მათი Account ბანკთან ინტეგრირებული ანგარიშია (D167; null, თუ ინტეგრაცია არ არის).
    //AddFluentValidation ამ კლასსაც DI-ში არეგისტრირებს, ამიტომ კონსტრუქტორი მხოლოდ რეგისტრირებულ სერვისებს იღებს
    public PaymentRequestValidator(IPaymentsRepository repository, IEnumerable<IBankMovementsClient> bankClients)
    {
        List<IntegratedBankAccount> integratedAccounts = [.. bankClients.Select(c => c.Account).OfType<IntegratedBankAccount>()];

        RuleFor(x => x.StudentContractId).MustAsync((id, ct) => repository.StudentContractExists(id, ct))
            .WithErrorCode(PaymentErrors.StudentContractNotFound.Code)
            .WithMessage(PaymentErrors.StudentContractNotFound.Description);

        RuleFor(x => x.PayDate).NotEmpty().WithErrorCode(PaymentErrors.PayDateIsRequired.Code)
            .WithMessage(PaymentErrors.PayDateIsRequired.Description);

        //უარყოფითი თანხა დასაშვებია: "გადატანა" კონტრაქტებს შორის, "შარშანდელი" ვალი
        RuleFor(x => x.Amount).NotEqual(0).WithErrorCode(PaymentErrors.AmountMustNotBeZero.Code)
            .WithMessage(PaymentErrors.AmountMustNotBeZero.Description);
        RuleFor(x => x.Amount).Must(HasAllowedDecimals).WithErrorCode(PaymentErrors.AmountHasTooManyDecimals.Code)
            .WithMessage(PaymentErrors.AmountHasTooManyDecimals.Description);

        //სიგრძე მოწმდება ისე, როგორც ინახება: trim-ის შემდეგ
        RuleFor(x => x.Document)
            .Must(document => (PaymentMapper.NormalizeText(document)?.Length ?? 0) <= DocumentMaxLength)
            .WithErrorCode(PaymentErrors.DocumentIsTooLong.Code)
            .WithMessage(PaymentErrors.DocumentIsTooLong.Description);

        RuleFor(x => x.BankAccountId).NotNull().WithErrorCode(PaymentErrors.BankAccountIsRequired.Code)
            .WithMessage(PaymentErrors.BankAccountIsRequired.Description);
        RuleFor(x => x.BankAccountId).MustAsync((id, ct) => repository.BankAccountExists(id!.Value, ct))
            .When(x => x.BankAccountId is not null).WithErrorCode(PaymentErrors.BankAccountNotFound.Code)
            .WithMessage(PaymentErrors.BankAccountNotFound.Description);

        //ინტეგრირებული ანგარიშის გადახდები გადასვლის დღიდან მხოლოდ ბანკიდან შემოდის (D164, D167); მანამდელი
        //(ისტორიული) გადახდები იცვლება როგორც ადრე
        RuleFor(x => x.BankAccountId)
            .Must((request, bankAccountId) => !integratedAccounts.Exists(account =>
                account.BankAccountId == bankAccountId && request.PayDate.Date >= account.StartDate.Date))
            .WithErrorCode(BankTransactionErrors.ManualPaymentOnIntegratedAccount.Code)
            .WithMessage(BankTransactionErrors.ManualPaymentOnIntegratedAccount.Description);
    }

    public static bool HasAllowedDecimals(decimal amount)
    {
        return decimal.Round(amount, AmountDecimals) == amount;
    }
}
