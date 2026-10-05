using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.Extensions.DependencyInjection;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CloseAcademicYearGroupsCommandHandler(
    IAcademicYearsRepository repository,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider) : ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>
{
    //თითო ჯგუფი ერთ ტრანზაქციაშია: VoidDate და გაკვეთილების ცვლილებები ერთი SaveChanges-ით (როგორც გენერატორში, თითო
    //ჯგუფი ცალკე scope-ში). უკვე დახურული ჯგუფები აღარ ირჩევა, ამიტომ განმეორებითი გაშვება არაფერს ცვლის
    public async Task<Result<CloseAcademicYearGroupsResponse>> Handle(CloseAcademicYearGroupsCommand command,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        AcademicYear? academicYear = academicYears.SingleOrDefault(ay => ay.AyId == command.AyId);
        if (academicYear is null)
        {
            return AcademicYearErrors.AcademicYearNotFound;
        }

        DateTime closeDate = (command.CloseDate ?? academicYear.FinishDate).Date;
        if (closeDate <= academicYear.StartDate)
        {
            return AcademicYearErrors.CloseDateIsOutOfRange;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(lessonGeneratorRepository,
            unitOfWork, now, command.DryRun, cancellationToken);
        List<GroupToClose> groupsToClose = await repository.GetGroupsToClose(command.AyId, closeDate,
            cancellationToken);

        List<CloseGroupResponse> groups = [];
        foreach (GroupToClose groupToClose in groupsToClose)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            GroupClosing? closing = null;
            GroupGenerationResult? result = await GroupLessonsGeneration.Run(
                scope.ServiceProvider.GetRequiredService<ILessonGeneratorRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), groupToClose.GrpId, command.DryRun, now,
                input => GroupLessonsPlanner.PlanGroup(input, horizon.HorizonEnd), cancellationToken,
                group => closing = GroupClosing.Apply(group, closeDate));
            //ჯგუფი შეიძლება ამასობაში წაიშალა: მაშინ Run-ი ჯგუფს არ ამზადებს და null-ს აბრუნებს, სხვა დროს ორივე არის
            if (result is null)
            {
                continue;
            }

            GroupClosing groupClosing = closing!;
            GroupLessonsGenerationResponse generation = result.Response;
            groups.Add(new CloseGroupResponse(groupToClose.GrpId, groupToClose.GroupCode, groupToClose.CourseName,
                groupClosing.PreviousVoidDate, groupClosing.OpenTeacherRowsCount, groupClosing.OpenStudentRowsCount,
                groupClosing.OpenScheduleRowsCount, groupClosing.DeletedLessonsCharges(generation.Changes
                    .Where(c => c.Action == LessonGeneratorMapper.DeleteAction).Select(c => c.LessonId)
                    .OfType<int>()), generation));
        }

        return new CloseAcademicYearGroupsResponse(command.DryRun, command.AyId, closeDate, horizon.HorizonEnd,
            groups);
    }
}
