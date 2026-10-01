using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.CrmCalls.Validation;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.UpdateCrmCall;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateCrmCallCommandHandler(ICrmCallsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCrmCallCommand>
{
    public async Task<Result> Handle(UpdateCrmCallCommand command, CancellationToken cancellationToken)
    {
        CrmCall? crmCall = await repository.GetForChange(command.CrmCallId, cancellationToken);
        if (crmCall is null)
        {
            return Result.Failure(CrmCallErrors.CrmCallNotFound);
        }

        CrmCallMapper.ApplyFields(crmCall, command.Request!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
