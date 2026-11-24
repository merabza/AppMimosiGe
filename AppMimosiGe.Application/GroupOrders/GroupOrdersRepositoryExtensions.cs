using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGeShared.Contracts.Errors;
using MimosiGeCore.Domain.Models;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders;

public static class GroupOrdersRepositoryExtensions
{
    /// <summary>
    ///     სამიზნე მდგომარეობის ჩაწერა (GROUP_ORDERS.md §5 ნაბიჯი 5, D198). states: შეხებული ჯგუფების მდგომარეობები
    ///     ჯგუფის Id-ით, გადათამაშებიდან (GroupOrdersReplayResult.States). newGroupId: ჯგუფის დროებითი Id, თუ შექმნის
    ///     ბრძანება ჯგუფს ახლა ქმნის; დანარჩენი ჯგუფები ბაზაშია და ერთ კონტექსტში იტვირთება. ყოველ ჯგუფს DirtyLessons
    ///     ერთვება, მის ძველ და ახალ მოსწავლეებს კი შემდეგი გადახდის თარიღის გადათვლა, როგორც ჯგუფის რედაქტორში (D56).
    ///     ბაზაში არაფერი იწერება: ცვლილებებს handler-ის ერთი SaveChanges ჩაწერს. აბრუნებს ჯგუფებს states-ის Id-ებით,
    ///     რომ შექმნის ბრძანება ახალ ჯგუფს navigation-ით დაუკავშირდეს
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, Group>> WriteStates(this IGroupOrdersRepository repository,
        IReadOnlyDictionary<int, GroupState> states, GroupOrderNewLines newLines, int? newGroupId,
        CancellationToken cancellationToken)
    {
        Dictionary<int, Group> groups = (await repository.GetGroupsForChange(
            [.. states.Keys.Where(id => id != newGroupId)], cancellationToken)).ToDictionary(g => g.GrpId);
        //ჩაწერამდელი მოსწავლეებიც: მოხსნილი სტრიქონის კონტრაქტის დარიცხვაც იცვლება
        List<int> studentContractIds =
            [.. groups.Values.SelectMany(g => g.GroupsByStudents).Select(s => s.StudentContractId)];

        foreach ((int groupId, GroupState state) in states)
        {
            if (groupId == newGroupId)
            {
                Group group = GroupStateWriter.Create(state, newLines);
                repository.AddGroup(group);
                groups[groupId] = group;
            }
            else
            {
                await repository.RemoveRows(GroupStateWriter.Apply(groups[groupId], state, newLines),
                    cancellationToken);
            }

            groups[groupId].DirtyLessons = true;
            studentContractIds.AddRange(state.Students.Select(s => s.StudentContractId));
        }

        await MarkNextPayDatesDirty(repository, studentContractIds, cancellationToken);
        return groups;
    }

    /// <summary>
    ///     ჯგუფის წაშლა მისი შექმნის ბრძანების წაშლისას (D191): გაკვეთილები, დასწრება, სტრიქონები, ჯგუფის ბრძანებები და
    ///     ჯგუფი. უარყოფილია, თუ ჯგუფის რომელიმე გაკვეთილი ხელფასის დათვლაშია. გადაყვანის კავშირს handler-ი ამოწმებს.
    ///     მოსწავლეების შემდეგი გადახდის თარიღი თავიდან ითვლება. ბაზაში არაფერი იწერება: წაშლას handler-ის SaveChanges
    ///     ჩაწერს
    /// </summary>
    public static async Task<Result> DeleteGroup(this IGroupOrdersRepository repository, int groupId,
        CancellationToken cancellationToken)
    {
        if (await repository.HasSalary(groupId, cancellationToken))
        {
            return Result.Failure(GroupOrderErrors.CreateOrderHasSalary);
        }

        Group group = (await repository.GetGroupsForChange([groupId], cancellationToken)).Single();
        await MarkNextPayDatesDirty(repository, group.GroupsByStudents.Select(s => s.StudentContractId),
            cancellationToken);
        await repository.RemoveGroup(group, cancellationToken);
        return Result.Success();
    }

    //Access-ის SetStudentNextPayDateDirtyForGroup: ჯგუფის ცვლილება მისი მოსწავლეების შემდეგი გადახდის თარიღს ცვლის
    private static async Task MarkNextPayDatesDirty(IGroupOrdersRepository repository,
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
