using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Validation;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.UpdateWorkHour;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateWorkHourCommandHandler(IWorkHoursRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateWorkHourCommand>
{
    public async Task<Result> Handle(UpdateWorkHourCommand command, CancellationToken cancellationToken)
    {
        WorkHour? workHour = await repository.GetForChange(command.WhId, cancellationToken);
        if (workHour is null)
        {
            return Result.Failure(WorkHourErrors.WorkHourNotFound);
        }

        WorkHourMapper.ApplyFields(workHour, command.Request!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
