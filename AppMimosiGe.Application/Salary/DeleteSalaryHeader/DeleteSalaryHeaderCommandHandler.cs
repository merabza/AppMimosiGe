using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.DeleteSalaryHeader;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteSalaryHeaderCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteSalaryHeaderCommand>
{
    //Access-ის კავშირები (კასკადის გარეშე) მდგენელებიან ან სტრიქონებიან უწყისს წაშლას არ აძლევდა
    public async Task<Result> Handle(DeleteSalaryHeaderCommand command, CancellationToken cancellationToken)
    {
        SalaryHeader? header = await repository.GetHeaderForChange(command.ShId, cancellationToken);
        if (header is null)
        {
            return Result.Failure(SalaryErrors.SalaryHeaderNotFound);
        }

        if (await repository.HeaderHasData(command.ShId, cancellationToken))
        {
            return Result.Failure(SalaryErrors.SalaryHeaderHasData);
        }

        repository.RemoveHeader(header);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
