using AppMimosiGe.Application.TeacherContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;

// ReSharper disable once UnusedType.Global
public sealed class UpdateTeacherContractCommandValidator : AbstractValidator<UpdateTeacherContractCommand>
{
    public UpdateTeacherContractCommandValidator(ITeacherContractsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new TeacherContractRequestValidator(repository));

            //შესაცვლელი კონტრაქტი თავის თავს დუბლიკატად არ ითვლის
            RuleFor(x => x)
                .MustAsync(async (command, ct) =>
                    !await repository.ContractNumberExists(command.Request!.ContractNumber!, command.Id, ct))
                .When(x => !string.IsNullOrEmpty(x.Request!.ContractNumber))
                .WithErrorCode(TeacherContractErrors.ContractNumberAlreadyExists.Code)
                .WithMessage(TeacherContractErrors.ContractNumberAlreadyExists.Description);
        });
    }
}
