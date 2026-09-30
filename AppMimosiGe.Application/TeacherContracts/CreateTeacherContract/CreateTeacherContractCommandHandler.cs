using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.TeacherContracts.Validation;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.TeacherContracts.CreateTeacherContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateTeacherContractCommandHandler(ITeacherContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateTeacherContractCommand, int>
{
    public async Task<Result<int>> Handle(CreateTeacherContractCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        var teacherContract = new TeacherContract();
        TeacherContractMapper.ApplyFields(teacherContract, command.Request!);

        repository.Add(teacherContract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return teacherContract.Id;
    }
}
