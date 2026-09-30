using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.DeleteTeacherContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteTeacherContractCommandHandler(ITeacherContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteTeacherContractCommand>
{
    public async Task<Result> Handle(DeleteTeacherContractCommand command, CancellationToken cancellationToken)
    {
        TeacherContract? teacherContract = await repository.GetForChange(command.Id, cancellationToken);
        if (teacherContract is null)
        {
            return Result.Failure(TeacherContractErrors.TeacherContractNotFound);
        }

        //ჯგუფი, გაკვეთილი, ხელფასი და სამუშაო საათები კონტრაქტს ბაზაში Restrict-ით ებმის
        if (await repository.IsInUse(command.Id, cancellationToken))
        {
            return Result.Failure(TeacherContractErrors.TeacherContractIsInUse);
        }

        repository.Remove(teacherContract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
