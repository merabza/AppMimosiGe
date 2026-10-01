using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GenerateGroupsLessonsCommandHandler(
    ILessonGeneratorRepository repository,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse>
{
    public async Task<Result<LessonsGenerationResponse>> Handle(GenerateGroupsLessonsCommand command,
        CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;
        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(repository, unitOfWork, now,
            command.DryRun, cancellationToken);

        //ახალი თვე ყველა ჯგუფს dirty-ს ხდის; dry-run-ში ის არ ჩაიწერა, ამიტომ ყველა ჯგუფი ისე მოწმდება, თითქოს dirty-ა
        bool onlyDirty = command.OnlyDirty && !(command.DryRun && horizon.AddedMonthsCount > 0);
        List<int> grpIds = await repository.GetGroupIds(onlyDirty, cancellationToken);

        List<GroupLessonsGenerationResponse> groups = [];
        foreach (int grpId in grpIds)
        {
            //თითო ჯგუფი ცალკე scope-ში: საკუთარი DbContext და ტრანზაქცია, წინა ჯგუფების entity-ები აღარ ითვალთვალება
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            GroupGenerationResult? result = await GroupLessonsGeneration.Run(
                scope.ServiceProvider.GetRequiredService<ILessonGeneratorRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), grpId, command.DryRun, now,
                input => GroupLessonsPlanner.PlanGroup(input, horizon.HorizonEnd), cancellationToken);
            //ჯგუფი შეიძლება ამასობაში წაიშალა
            if (result is not null)
            {
                groups.Add(result.Response);
            }
        }

        return new LessonsGenerationResponse(command.DryRun, horizon.HorizonEnd, horizon.AddedMonthsCount, groups);
    }
}
