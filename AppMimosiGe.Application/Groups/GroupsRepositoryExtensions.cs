using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups;

public static class GroupsRepositoryExtensions
{
    /// <summary>
    ///     Access-ის TeacherContractID_Change: მასწავლებლის არჩევისას სქემა ხდება მისი კონტრაქტის ძირითადი სქემა
    ///     (SalarySchemaByHours). აბრუნებს ამ სქემებს იმ კონტრაქტებისთვის, რომელთა სტრიქონიც სქემის გარეშე მოვიდა
    /// </summary>
    public static async Task<Dictionary<int, int>> GetDefaultSalarySchemes(this IGroupsRepository repository,
        IEnumerable<GroupTeacherRequest> teachers, CancellationToken cancellationToken)
    {
        var defaultSalarySchemes = new Dictionary<int, int>();
        foreach (int teacherContractId in teachers.Where(x => x.SalarySchemaId is null).Select(x => x.TeacherContractId)
                     .Distinct())
        {
            //ვალიდატორმა უკვე შეამოწმა, რომ კონტრაქტს ძირითადი სქემა აქვს
            defaultSalarySchemes[teacherContractId] =
                await repository.GetDefaultSalarySchemeId(teacherContractId, cancellationToken) ??
                throw new InvalidOperationException(
                    $"Teacher contract {teacherContractId} has no default salary scheme");
        }

        return defaultSalarySchemes;
    }

    /// <summary>
    ///     მასწავლებლების დროების შემოწმება შენახვამდე (GroupScheduleOverlapsRule, D178): ჯგუფის მასწავლებლების განრიგი
    ///     შენახვის შემდეგ მათ სხვა ჯგუფებს ედრება. აბრუნებს შეცდომას გადაფარვების აღწერებით, ან null-ს, თუ გადაფარვა არ არის
    /// </summary>
    public static async Task<Error?> FindTeacherTimesOverlap(this IGroupsRepository repository,
        GroupSchedule schedule, CancellationToken cancellationToken)
    {
        List<GroupSchedule> otherSchedules = await repository.GetTeacherSchedules(ContractIds(schedule),
            schedule.GrpId, cancellationToken);
        IReadOnlyList<GroupScheduleOverlap> overlaps = GroupScheduleOverlapsRule.Find(schedule, otherSchedules);
        if (overlaps.Count == 0)
        {
            return null;
        }

        Dictionary<int, string> teacherNames =
            (await repository.GetTeacherContracts(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        return GroupErrors.TeacherTimesOverlap(
            await DescribeOverlaps(repository, overlaps, teacherNames, cancellationToken));
    }

    /// <summary>
    ///     მოსწავლეების დროების შემოწმება შენახვამდე (GroupScheduleOverlapsRule, D179): ჯგუფის მოსწავლეების განრიგი
    ///     შენახვის შემდეგ მათ სხვა ჯგუფებს ედრება. აბრუნებს შეცდომას გადაფარვების აღწერებით, ან null-ს, თუ გადაფარვა არ არის
    /// </summary>
    public static async Task<Error?> FindStudentTimesOverlap(this IGroupsRepository repository,
        GroupSchedule schedule, CancellationToken cancellationToken)
    {
        List<GroupSchedule> otherSchedules = await repository.GetStudentSchedules(ContractIds(schedule),
            schedule.GrpId, cancellationToken);
        IReadOnlyList<GroupScheduleOverlap> overlaps = GroupScheduleOverlapsRule.Find(schedule, otherSchedules);
        if (overlaps.Count == 0)
        {
            return null;
        }

        Dictionary<int, string> studentNames = await repository.GetStudentContractNames(
            [.. overlaps.Select(o => o.ContractId).Distinct()], cancellationToken);
        return GroupErrors.StudentTimesOverlap(
            await DescribeOverlaps(repository, overlaps, studentNames, cancellationToken));
    }

    //ჯგუფის მასწავლებლების ან მოსწავლეების კონტრაქტები, თითო ერთხელ
    private static int[] ContractIds(GroupSchedule schedule)
    {
        return [.. schedule.Members.Select(x => x.ContractId).Distinct()];
    }

    //გადაფარვების აღწერები იმავე რიგით: წევრის სახელი (კონტრაქტის ID-ით) და კვირის დღის სახელი
    private static async Task<List<string>> DescribeOverlaps(IGroupsRepository repository,
        IReadOnlyList<GroupScheduleOverlap> overlaps, Dictionary<int, string> memberNames,
        CancellationToken cancellationToken)
    {
        Dictionary<int, string> weekDayNames =
            (await repository.GetWeekDays(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        return [.. overlaps.Select(o => o.Describe(memberNames[o.ContractId], weekDayNames[o.WeekDayId]))];
    }

    /// <summary>
    ///     Access-ის SetStudentNextPayDateDirtyForGroup: ჯგუფის ცვლილება მისი მოსწავლეების შემდეგი გადახდის თარიღს
    ///     ცვლის, ამიტომ ამ კონტრაქტებს DirtyNextPayDate ერთვება. ალამი იმავე SaveChanges-ით (ერთ ტრანზაქციაში) ინახება
    /// </summary>
    public static async Task MarkNextPayDatesDirty(this IGroupsRepository repository,
        IEnumerable<int> studentContractIds, CancellationToken cancellationToken)
    {
        int[] ids = [.. studentContractIds.Distinct()];
        if (ids.Length == 0)
        {
            return;
        }

        foreach (StudentContract studentContract in await repository.GetStudentContractsForChange(ids,
                     cancellationToken))
        {
            studentContract.DirtyNextPayDate = true;
        }
    }
}
