using AppMimosiGe.Application.WorkHours.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.Validation;

/// <summary>
///     "სამუშაოს დაწყების" და "დასრულების" მოთხოვნა (Access-ის cmdFixWorkStart / cmdFixWorkEnd): თანამშრომელი არჩეული
///     უნდა იყოს და სამუშაო საათების ჯგუფის მქონე კონტრაქტი უნდა იყოს, ლუფტი 30 წუთზე მეტი ვერ იქნება
/// </summary>
public sealed class WorkTimeFixRequestValidator : AbstractValidator<WorkTimeFixRequest>
{
    public WorkTimeFixRequestValidator(IWorkHoursRepository repository)
    {
        RuleFor(x => x.TeacherContractId).NotNull().WithErrorCode(WorkHourErrors.EmployeeIsRequired.Code)
            .WithMessage(WorkHourErrors.EmployeeIsRequired.Description);
        RuleFor(x => x.TeacherContractId).MustAsync((id, ct) => repository.EmployeeExists(id!.Value, ct))
            .When(x => x.TeacherContractId is not null).WithErrorCode(WorkHourErrors.EmployeeNotFound.Code)
            .WithMessage(WorkHourErrors.EmployeeNotFound.Description);

        RuleFor(x => x.LuftMinutes).LessThanOrEqualTo(WorkTimeFixRules.MaxLuftMinutes)
            .WithErrorCode(WorkHourErrors.LuftIsTooBig.Code).WithMessage(WorkHourErrors.LuftIsTooBig.Description);
    }
}
