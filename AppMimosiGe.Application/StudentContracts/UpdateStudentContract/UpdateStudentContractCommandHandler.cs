using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.UpdateStudentContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateStudentContractCommandHandler(IStudentContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateStudentContractCommand>
{
    public async Task<Result> Handle(UpdateStudentContractCommand command, CancellationToken cancellationToken)
    {
        StudentContractRequest request = command.Request!;

        StudentContract? studentContract = await repository.GetForChange(command.ScId, cancellationToken);
        if (studentContract is null)
        {
            return Result.Failure(StudentContractErrors.StudentContractNotFound);
        }

        if (!StudentContractMapper.SyncDetails(studentContract, request.Details))
        {
            return Result.Failure(StudentContractErrors.DetailNotFound);
        }

        StudentContractMapper.ApplyFields(studentContract, request);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
