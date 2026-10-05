using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.Models;
using MimosiGeCore.Domain.Models;
using SystemTools.Domain.Abstractions;

namespace AppMimosiGe.Application.LessonGenerator;

/// <summary>
///     გენერატორის ნაბიჯები, რომლებსაც ყველა handler-ი იყენებს
/// </summary>
public static class GroupLessonsGeneration
{
    /// <summary>
    ///     Access-ის CheckOperationMonths, რომელიც ყოველ გენერაციამდე ეშვება: საჭიროებისას სამუშაო თვეები ემატება და ყველა
    ///     ჯგუფი და კონტრაქტი dirty ხდება (SetAllDirty). dry-run-ში არაფერი იწერება, ჰორიზონტი კი ისე ითვლება, თითქოს
    ///     თვეები დაემატა
    /// </summary>
    public static async Task<GenerationHorizon> PrepareOperationMonths(ILessonGeneratorRepository repository,
        IUnitOfWork unitOfWork, DateTime now, bool dryRun, CancellationToken cancellationToken)
    {
        DateTime? lastMonth = await repository.GetLastOperationMonth(cancellationToken);
        IReadOnlyList<DateTime> monthsToAdd = OperationMonthsCalendar.MonthsToAdd(lastMonth, now);
        if (monthsToAdd.Count > 0 && !dryRun)
        {
            repository.AddOperationMonths(monthsToAdd);
            await repository.MarkAllDirty(cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        //თვე ემატება, თუ კალენდარი ცარიელია, ამიტომ ბოლო თვე ყოველთვის არის
        DateTime horizonMonth = monthsToAdd.Count > 0 ? monthsToAdd[^1] : lastMonth!.Value;
        return new GenerationHorizon(OperationMonthsCalendar.HorizonEnd(horizonMonth), monthsToAdd.Count);
    }

    /// <summary>
    ///     ერთი ჯგუფის გენერაცია ერთ ტრანზაქციაში: ჯგუფის მონაცემები და გაკვეთილები ერთად იტვირთება, გეგმა ითვლება და
    ///     ყველა ცვლილება ერთი SaveChanges-ით იწერება. dry-run-ში მხოლოდ გეგმა ბრუნდება. null: ჯგუფი არ არსებობს.
    ///     prepareGroup: ჯგუფის ცვლილება გეგმამდე (მაგ. სასწავლო წლის დახურვის VoidDate), რომელიც იმავე ტრანზაქციაში იწერება;
    ///     dry-run-ში მხოლოდ მეხსიერებაში რჩება
    /// </summary>
    public static async Task<GroupGenerationResult?> Run(ILessonGeneratorRepository repository, IUnitOfWork unitOfWork,
        int grpId, bool dryRun, DateTime now, Func<GroupLessonsInput, GroupLessonsPlan> planner,
        CancellationToken cancellationToken, Action<Group>? prepareGroup = null)
    {
        Group? group = await repository.GetGroupForGeneration(grpId, !dryRun, cancellationToken);
        if (group is null)
        {
            return null;
        }

        prepareGroup?.Invoke(group);
        GroupLessonsPlan plan = planner(LessonGeneratorMapper.ToInput(group));
        IReadOnlyDictionary<DateTime, Lesson> createdLessons = new Dictionary<DateTime, Lesson>();
        if (!dryRun)
        {
            List<StudentContract> dirtyStudentContracts =
                await repository.GetStudentContractsForChange(plan.DirtyStudentContractIds, cancellationToken);
            createdLessons = LessonGeneratorMapper.ApplyPlan(group, plan, dirtyStudentContracts, repository, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        Dictionary<int, string> errorTexts = await repository.GetErrorLogTexts(cancellationToken);
        return new GroupGenerationResult(LessonGeneratorMapper.ToResponse(group, plan, errorTexts, createdLessons),
            plan.LastLesson, LastLessonId(plan.LastLesson, createdLessons));
    }

    private static int? LastLessonId(PlannedLastLesson? lastLesson,
        IReadOnlyDictionary<DateTime, Lesson> createdLessons)
    {
        if (lastLesson is null)
        {
            return null;
        }

        //ახალი გაკვეთილის ID მხოლოდ შენახვის შემდეგ ჩნდება
        return lastLesson.LessonId ?? createdLessons.GetValueOrDefault(lastLesson.LessonDt)?.Id;
    }
}
