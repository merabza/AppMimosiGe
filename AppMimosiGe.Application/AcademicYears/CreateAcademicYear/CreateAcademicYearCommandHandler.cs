using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.AcademicYears.CreateAcademicYear;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateAcademicYearCommandHandler(
    IAcademicYearsRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<CreateAcademicYearCommand, NewAcademicYearResponse>
{
    //ახალი წელი მიმდინარეს მომდევნოა, ამიტომ განმეორებით გაშვება იმავე წელს იძლევა: preview ამას აჩვენებს, შექმნა კი
    //409-ია
    public async Task<Result<NewAcademicYearResponse>> Handle(CreateAcademicYearCommand command,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        NextAcademicYear? next = NextAcademicYear.Plan(academicYears, timeProvider.GetLocalNow().Date);
        if (next is null)
        {
            return AcademicYearErrors.NoAcademicYears;
        }

        bool alreadyExists = next.ExistsIn(academicYears);
        string prefix = ContractNumbers.Prefix(next.StartDate);
        if (command.DryRun)
        {
            return new NewAcademicYearResponse(true, null, next.AcademicYearName, next.StartDate, next.FinishDate,
                prefix, alreadyExists);
        }

        if (alreadyExists)
        {
            return AcademicYearErrors.AcademicYearAlreadyExists;
        }

        AcademicYear academicYear = new()
        {
            AcademicYearName = next.AcademicYearName, StartDate = next.StartDate, FinishDate = next.FinishDate
        };
        repository.Add(academicYear);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NewAcademicYearResponse(false, academicYear.AyId, next.AcademicYearName, next.StartDate,
            next.FinishDate, prefix, false);
    }
}
