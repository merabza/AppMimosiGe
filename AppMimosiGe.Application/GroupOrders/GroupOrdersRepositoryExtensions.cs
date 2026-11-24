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
    ///     ბრძანებები გადათამაშებისთვის (D185, D205): groupIds-ის და მათთან გადაყვანებით, ჯაჭვით, დაკავშირებული ყველა
    ///     ჯგუფის ყველა ბრძანება, ნებისმიერი თარიღით. ჯაჭვს ყოველი ახალი რგოლი ერთი წაკითხვით აგრძელებს
    /// </summary>
    public static async Task<List<GroupOrderData>> GetLinkedOrders(this IGroupOrdersRepository repository,
        IEnumerable<int> groupIds, CancellationToken cancellationToken)
    {
        HashSet<int> loadedGroupIds = [.. groupIds];
        List<GroupOrderData> orders = await repository.GetOrders(loadedGroupIds, cancellationToken);
        int[] linkedGroupIds = LinkedGroupIds(orders, loadedGroupIds);
        while (linkedGroupIds.Length > 0)
        {
            loadedGroupIds.UnionWith(linkedGroupIds);
            orders = await repository.GetOrders(loadedGroupIds, cancellationToken);
            linkedGroupIds = LinkedGroupIds(orders, loadedGroupIds);
        }

        return orders;
    }

    /// <summary>
    ///     ჯგუფის წაშლა მისი შექმნის ბრძანების წაშლისას (D191) ან ხელახლა დაპაუზებისას (D213): გაკვეთილები, დასწრება,
    ///     სტრიქონები, ჯგუფის ბრძანებები (keptOrderId-ის გარდა: ხელახლა დაპაუზებული შექმნის ბრძანება რჩება) და ჯგუფი.
    ///     უარყოფილია, თუ ჯგუფის რომელიმე გაკვეთილი ხელფასის დათვლაშია. გადაყვანის კავშირს handler-ი ამოწმებს.
    ///     მოსწავლეების შემდეგი გადახდის თარიღი თავიდან ითვლება. ბაზაში არაფერი იწერება: წაშლას handler-ის SaveChanges
    ///     ჩაწერს
    /// </summary>
    public static async Task<Result> DeleteGroup(this IGroupOrdersRepository repository, int groupId,
        int? keptOrderId, CancellationToken cancellationToken)
    {
        if (await repository.HasSalary(groupId, cancellationToken))
        {
            return Result.Failure(GroupOrderErrors.CreateOrderHasSalary);
        }

        Group group = (await repository.GetGroupsForChange([groupId], cancellationToken)).Single();
        await MarkNextPayDatesDirty(repository, group.GroupsByStudents.Select(s => s.StudentContractId),
            cancellationToken);
        await repository.RemoveGroup(group, keptOrderId, cancellationToken);
        return Result.Success();
    }

    //გადაყვანებით დაკავშირებული ჯგუფები, რომელთა ბრძანებებიც ჯერ არ წაკითხულა
    private static int[] LinkedGroupIds(IEnumerable<GroupOrderData> orders, HashSet<int> loadedGroupIds)
    {
        return
        [
            .. orders.Where(o => o is { OrderType: EGroupOrderType.TransferStudents, IsPaused: false })
                .SelectMany(o => new[] { o.GroupId, o.TargetGroupId }).OfType<int>()
                .Where(id => !loadedGroupIds.Contains(id)).Distinct()
        ];
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
