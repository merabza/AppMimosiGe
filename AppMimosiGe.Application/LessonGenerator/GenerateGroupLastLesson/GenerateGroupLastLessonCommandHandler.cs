using System;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupLastLesson;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GenerateGroupLastLessonCommandHandler(
    ILessonGeneratorRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<GenerateGroupLastLessonCommand, GroupLastLessonResponse>
{
    public async Task<Result<GroupLastLessonResponse>> Handle(GenerateGroupLastLessonCommand command,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;
        GenerationHorizon horizon =
            await GroupLessonsGeneration.PrepareOperationMonths(repository, unitOfWork, now, false, cancellationToken);
        GroupGenerationResult? result = await GroupLessonsGeneration.Run(repository, unitOfWork, command.GrpId,
            false, now, input => GroupLessonsPlanner.PlanLastLesson(input, horizon.HorizonEnd, now.Date),
            cancellationToken);
        if (result is null)
        {
            return GroupErrors.GroupNotFound;
        }

        return new GroupLastLessonResponse(result.LastLessonId, result.LastLesson?.LessonDt,
            new LessonsGenerationResponse(false, horizon.HorizonEnd, horizon.AddedMonthsCount, [result.Response]));
    }
}
