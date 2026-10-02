using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.WorkHours.Validation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.WorkHours.CreateWorkHour;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateWorkHourCommandHandler(IWorkHoursRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateWorkHourCommand, int>
{
    public async Task<Result<int>> Handle(CreateWorkHourCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        var workHour = new WorkHour();
        WorkHourMapper.ApplyFields(workHour, command.Request!);
        repository.Add(workHour);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return workHour.WhId;
    }
}
