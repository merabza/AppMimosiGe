using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Salary.Validation;

/// <summary>
///     უწყისის ორივე თარიღი სავალდებულოა (Access-ის REQ)
/// </summary>
public sealed class SalaryHeaderRequestValidator : AbstractValidator<SalaryHeaderRequest>
{
    public SalaryHeaderRequestValidator()
    {
        RuleFor(x => x.ShChargeDate).NotEmpty().WithErrorCode(SalaryErrors.ChargeDateIsRequired.Code)
            .WithMessage(SalaryErrors.ChargeDateIsRequired.Description);

        RuleFor(x => x.ShTransferDate).NotEmpty().WithErrorCode(SalaryErrors.TransferDateIsRequired.Code)
            .WithMessage(SalaryErrors.TransferDateIsRequired.Description);
    }
}
