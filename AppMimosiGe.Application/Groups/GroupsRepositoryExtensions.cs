using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

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
