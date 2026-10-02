using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using FluentValidation;

namespace AppMimosiGe.Application.WorkHours.Validation;

/// <summary>
///     ჩანაწერის ველების შემოწმება, რომელიც შექმნასაც და შეცვლასაც სჭირდება: თანამშრომელი სამუშაო საათების ჯგუფის
///     მქონე კონტრაქტია (Access-ის ჩამოსაშლელი სია), დაწყება სავალდებულოა (Access-ის REQ), დასრულება არასავალდებულოა,
///     მაგრამ თუ არის, დაწყებაზე გვიან უნდა იყოს (Access-ში ეს არ მოწმდებოდა)
/// </summary>
public sealed class WorkHourRequestValidator : AbstractValidator<WorkHourRequest>
{
    public WorkHourRequestValidator(IWorkHoursRepository repository)
    {
        RuleFor(x => x.TeacherContractId).MustAsync((id, ct) => repository.EmployeeExists(id, ct))
            .WithErrorCode(WorkHourErrors.EmployeeNotFound.Code)
            .WithMessage(WorkHourErrors.EmployeeNotFound.Description);

        RuleFor(x => x.WhStart).NotEmpty().WithErrorCode(WorkHourErrors.StartIsRequired.Code)
            .WithMessage(WorkHourErrors.StartIsRequired.Description);

        RuleFor(x => x.WhEnd).Must((request, end) => end > request.WhStart).When(x => x.WhEnd is not null)
            .WithErrorCode(WorkHourErrors.EndMustBeAfterStart.Code)
            .WithMessage(WorkHourErrors.EndMustBeAfterStart.Description);
    }
}
