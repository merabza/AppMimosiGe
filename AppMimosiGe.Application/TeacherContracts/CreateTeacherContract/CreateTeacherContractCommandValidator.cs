using AppMimosiGe.Application.TeacherContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using BackendCarcassShared.Contracts.Errors;
using FluentValidation;

namespace AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;

// ReSharper disable once UnusedType.Global
public sealed class CreateTeacherContractCommandValidator : AbstractValidator<CreateTeacherContractCommand>
{
    public CreateTeacherContractCommandValidator(ITeacherContractsRepository repository)
    {
        RuleFor(x => x.Request).NotNull().WithErrorCode(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Code)
            .WithMessage(CrudApiErrors.UploadedInformationCouldNotBeDecrypted.Description);

        When(x => x.Request is not null, () =>
        {
            RuleFor(x => x.Request!).SetValidator(new TeacherContractRequestValidator(repository));

            RuleFor(x => x.Request!).MustAsync(async (request, ct) =>
                    !await repository.ContractNumberExists(request.ContractNumber!, 0, ct))
                .When(x => !string.IsNullOrEmpty(x.Request!.ContractNumber))
                .WithErrorCode(TeacherContractErrors.ContractNumberAlreadyExists.Code)
                .WithMessage(TeacherContractErrors.ContractNumberAlreadyExists.Description);
        });
    }
}
