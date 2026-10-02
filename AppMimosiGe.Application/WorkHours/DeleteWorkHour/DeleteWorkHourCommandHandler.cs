using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.DeleteWorkHour;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteWorkHourCommandHandler(IWorkHoursRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteWorkHourCommand>
{
    //Access-ის FrmWorkHours (datasheet) ჩანაწერის წაშლას ზღუდვის გარეშე იძლეოდა
    public async Task<Result> Handle(DeleteWorkHourCommand command, CancellationToken cancellationToken)
    {
        WorkHour? workHour = await repository.GetForChange(command.WhId, cancellationToken);
        if (workHour is null)
        {
            return Result.Failure(WorkHourErrors.WorkHourNotFound);
        }

        repository.Remove(workHour);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
