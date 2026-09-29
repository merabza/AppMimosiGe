using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Validation;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.StudentContracts.CreateStudentContract;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateStudentContractCommandHandler(IStudentContractsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateStudentContractCommand, int>
{
    public async Task<Result<int>> Handle(CreateStudentContractCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        StudentContractRequest request = command.Request!;

        var studentContract = new StudentContract { ContractNumber = request.ContractNumber! };
        StudentContractMapper.ApplyFields(studentContract, request);
        foreach (StudentContractDetailRequest detailRequest in request.Details)
        {
            studentContract.StudentContractDetails.Add(StudentContractMapper.CreateDetail(detailRequest));
        }

        repository.Add(studentContract);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return studentContract.ScId;
    }
}
