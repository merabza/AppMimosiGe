using System.Linq;
using AppMimosiGe.Application.Payments;
using AppMimosiGe.Application.Payments.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.BankTransactions.SetBankTransactionAllocation;

/// <summary>
///     წილების შემოწმება: თანხა დადებითია და 2 ათწილადამდე, კონტრაქტი არსებობს და განაწილებაში ერთხელაა. ჯამის
///     ზღვარს (≤ ჩარიცხული) და შემოწმებულ გადახდებს handler-ი ამოწმებს (409)
/// </summary>
// ReSharper disable once UnusedType.Global
public sealed class SetBankTransactionAllocationCommandValidator
    : AbstractValidator<SetBankTransactionAllocationCommand>
{
    public SetBankTransactionAllocationCommandValidator(IPaymentsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!.Shares)
                .Must(shares => shares.Select(s => s.StudentContractId).Distinct().Count() == shares.Count)
                .WithErrorCode(BankTransactionErrors.ShareStudentContractIsRepeated.Code)
                .WithMessage(BankTransactionErrors.ShareStudentContractIsRepeated.Description);

            RuleForEach(x => x.Request!.Shares).ChildRules(share =>
            {
                share.RuleFor(s => s.Amount).GreaterThan(0)
                    .WithErrorCode(BankTransactionErrors.ShareAmountMustBePositive.Code)
                    .WithMessage(BankTransactionErrors.ShareAmountMustBePositive.Description);
                share.RuleFor(s => s.Amount).Must(PaymentRequestValidator.HasAllowedDecimals)
                    .WithErrorCode(BankTransactionErrors.ShareAmountHasTooManyDecimals.Code)
                    .WithMessage(BankTransactionErrors.ShareAmountHasTooManyDecimals.Description);
                share.RuleFor(s => s.StudentContractId)
                    .MustAsync((id, ct) => repository.StudentContractExists(id, ct))
                    .WithErrorCode(BankTransactionErrors.ShareStudentContractNotFound.Code)
                    .WithMessage(BankTransactionErrors.ShareStudentContractNotFound.Description);
            });
        });
    }
}
