using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.DeleteStudentContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DeleteStudentContractCommandHandler(IStudentContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteStudentContractCommand>
{
    public async Task<Result> Handle(DeleteStudentContractCommand command, CancellationToken cancellationToken)
    {
        StudentContract? studentContract = await repository.GetForChange(command.ScId, cancellationToken);
        if (studentContract is null)
        {
            return Result.Failure(StudentContractErrors.StudentContractNotFound);
        }

        //ჯგუფი, გაკვეთილი, გადახდა ან CRM ზარი კონტრაქტს ბაზაში Restrict-ით ებმის. დეტალები კი Cascade-ით იშლება
        if (await repository.IsInUse(command.ScId, cancellationToken))
        {
            return Result.Failure(StudentContractErrors.StudentContractIsInUse);
        }

        repository.Remove(studentContract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
