using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGe.Application.Salary.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.UpdateSalaryPart;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateSalaryPartCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateSalaryPartCommand>
{
    //უწყისი არ იცვლება; გამოთვლით შექმნილი მდგენელი (ტიპი 1) ხელით არ იცვლება
    public async Task<Result> Handle(UpdateSalaryPartCommand command, CancellationToken cancellationToken)
    {
        SalaryPart? part = await repository.GetPartForChange(command.SpId, cancellationToken);
        if (part is null)
        {
            return Result.Failure(SalaryErrors.SalaryPartNotFound);
        }

        if (part.SalaryPartTypeId == SalaryCalculator.LessonSalaryPartTypeId)
        {
            return Result.Failure(SalaryErrors.PartIsCalculated);
        }

        SalaryPartRequest request = command.Request!;
        Error? error = await SalaryPartRules.Check(repository, request, cancellationToken);
        if (error is not null)
        {
            return Result.Failure(error);
        }

        part.TeacherContractId = request.TeacherContractId;
        part.SalaryPartTypeId = request.SalaryPartTypeId;
        part.SpAmount = request.SpAmount;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
