using AppMimosiGe.Application.StudentContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.StudentContracts.CreateStudentContract;

// ReSharper disable once UnusedType.Global
public sealed class CreateStudentContractCommandValidator : AbstractValidator<CreateStudentContractCommand>
{
    public CreateStudentContractCommandValidator(IStudentContractsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new StudentContractRequestValidator(repository));

            RuleFor(x => x.Request!)
                .MustAsync(async (request, ct) =>
                    !await repository.ContractNumberExists(request.AcademicYearId, request.ContractNumber!, 0, ct))
                .When(x => !string.IsNullOrEmpty(x.Request!.ContractNumber))
                .WithErrorCode(StudentContractErrors.ContractNumberAlreadyExists.Code)
                .WithMessage(StudentContractErrors.ContractNumberAlreadyExists.Description);

            //მოსწავლის ბალანსი მისი ყველა კონტრაქტის ჯამია, ამიტომ წელში მოსწავლეს ერთი კონტრაქტი აქვს (ნაწილი 20)
            RuleFor(x => x.Request!)
                .MustAsync(async (request, ct) =>
                    !await repository.StudentHasContract(request.AcademicYearId, request.StudentHumanId, 0, ct))
                .WithErrorCode(StudentContractErrors.StudentAlreadyHasContract.Code)
                .WithMessage(StudentContractErrors.StudentAlreadyHasContract.Description);
        });
    }
}
