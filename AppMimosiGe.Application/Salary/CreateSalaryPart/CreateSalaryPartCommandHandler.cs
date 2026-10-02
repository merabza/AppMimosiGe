using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.CreateSalaryPart;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateSalaryPartCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSalaryPartCommand, int>
{
    //Access-ში მდგენელები ცხრილში პირდაპირ იწერებოდა (ფორმა არ ჰქონდა)
    public async Task<Result<int>> Handle(CreateSalaryPartCommand command, CancellationToken cancellationToken)
    {
        if (!await repository.HeaderExists(command.ShId, cancellationToken))
        {
            return SalaryErrors.SalaryHeaderNotFound;
        }

        SalaryPartRequest request = command.Request!;
        Error? error = await SalaryPartRules.Check(repository, request, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var part = new SalaryPart
        {
            ShId = command.ShId,
            TeacherContractId = request.TeacherContractId,
            SalaryPartTypeId = request.SalaryPartTypeId,
            SpAmount = request.SpAmount
        };
        repository.AddPart(part);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return part.SpId;
    }
}
