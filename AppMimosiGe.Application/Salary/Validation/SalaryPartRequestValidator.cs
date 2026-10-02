using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.Salary.Validation;

/// <summary>
///     მდგენელის ველები: თანამშრომლის კონტრაქტი არსებობს (Access-ის ჩამოსაშლელი სია ყველა კონტრაქტს აჩვენებდა),
///     ტიპი არჩეულია. ტიპის წესებს (ტიპი 1, გამოქვითვის ნიშანი) SalaryPartRules ამოწმებს
/// </summary>
public sealed class SalaryPartRequestValidator : AbstractValidator<SalaryPartRequest>
{
    public SalaryPartRequestValidator(ISalaryRepository repository)
    {
        RuleFor(x => x.TeacherContractId).MustAsync((id, ct) => repository.EmployeeExists(id, ct))
            .WithErrorCode(SalaryErrors.EmployeeNotFound.Code).WithMessage(SalaryErrors.EmployeeNotFound.Description);

        RuleFor(x => x.SalaryPartTypeId).NotNull().WithErrorCode(SalaryErrors.PartTypeIsRequired.Code)
            .WithMessage(SalaryErrors.PartTypeIsRequired.Description);
    }
}
