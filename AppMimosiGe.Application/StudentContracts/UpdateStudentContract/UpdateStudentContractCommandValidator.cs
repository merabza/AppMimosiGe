using AppMimosiGe.Application.StudentContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.StudentContracts.UpdateStudentContract;

// ReSharper disable once UnusedType.Global
public sealed class UpdateStudentContractCommandValidator : AbstractValidator<UpdateStudentContractCommand>
{
    public UpdateStudentContractCommandValidator(IStudentContractsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new StudentContractRequestValidator(repository));

            //შესაცვლელი კონტრაქტი თავის თავს დუბლიკატად არ ითვლის
            RuleFor(x => x)
                .MustAsync(async (command, ct) =>
                    !await repository.ContractNumberExists(command.Request!.AcademicYearId,
                        command.Request.ContractNumber!, command.ScId, ct))
                .When(x => !string.IsNullOrEmpty(x.Request!.ContractNumber))
                .WithErrorCode(StudentContractErrors.ContractNumberAlreadyExists.Code)
                .WithMessage(StudentContractErrors.ContractNumberAlreadyExists.Description);
        });
    }
}
