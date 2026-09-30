using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.UpdateTeacherContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateTeacherContractCommandHandler(ITeacherContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTeacherContractCommand>
{
    public async Task<Result> Handle(UpdateTeacherContractCommand command, CancellationToken cancellationToken)
    {
        TeacherContract? teacherContract = await repository.GetForChange(command.Id, cancellationToken);
        if (teacherContract is null)
        {
            return Result.Failure(TeacherContractErrors.TeacherContractNotFound);
        }

        TeacherContractMapper.ApplyFields(teacherContract, command.Request!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
