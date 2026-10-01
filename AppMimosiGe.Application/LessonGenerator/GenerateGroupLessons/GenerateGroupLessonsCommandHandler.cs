using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupLessons;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GenerateGroupLessonsCommandHandler(
    ILessonGeneratorRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<GenerateGroupLessonsCommand, LessonsGenerationResponse>
{
    public async Task<Result<LessonsGenerationResponse>> Handle(GenerateGroupLessonsCommand command,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;
        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(repository, unitOfWork, now,
            command.DryRun, cancellationToken);
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(repository, unitOfWork, command.GrpId,
            command.DryRun, now, input => GroupLessonsPlanner.PlanGroup(input, horizon.HorizonEnd), cancellationToken);
        if (result is null)
        {
            return GroupErrors.GroupNotFound;
        }

        return new LessonsGenerationResponse(command.DryRun, horizon.HorizonEnd, horizon.AddedMonthsCount,
            [result.Response]);
    }
}
