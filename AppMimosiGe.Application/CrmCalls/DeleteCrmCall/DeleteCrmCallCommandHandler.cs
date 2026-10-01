using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.CrmCalls.DeleteCrmCall;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteCrmCallCommandHandler(ICrmCallsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteCrmCallCommand>
{
    //Access-ის FrmCRMCalls (datasheet) ზარის წაშლას ზღუდვის გარეშე იძლეოდა
    public async Task<Result> Handle(DeleteCrmCallCommand command, CancellationToken cancellationToken)
    {
        CrmCall? crmCall = await repository.GetForChange(command.CrmCallId, cancellationToken);
        if (crmCall is null)
        {
            return Result.Failure(CrmCallErrors.CrmCallNotFound);
        }

        repository.Remove(crmCall);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
