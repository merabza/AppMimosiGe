using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.GroupOrders;

/// <summary>
///     ჯგუფების ბრძანებების რეპოზიტორია (GROUP_ORDERS.md §5, §6). რეალიზაცია AppMimosiGe.Infrastructure-შია. ცვლილებები
///     ერთ კონტექსტში გროვდება და handler-ის ერთი SaveChanges-ით, ერთ ტრანზაქციაში იწერება: შეხებული ჯგუფები, მთვლელი
///     და ბრძანება
/// </summary>
public interface IGroupOrdersRepository
{
    //ჯგუფების ბრძანებები ხაზებით, ძრავისთვის: ბრძანება, რომლის ჯგუფი ან გადაყვანის სამიზნე groupIds-შია, თარიღისა და
    //Id-ის რიგით. დაპაუზებულ შექმნის ბრძანებას ჯგუფი არ აქვს, ამიტომ აქ არ ხვდება
    Task<List<GroupOrderData>> GetOrders(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default);

    //ჩასაწერად ან წასაშლელად: ჯგუფები მასწავლებლების, მოსწავლეებისა და განრიგის სტრიქონებით, EF-ის თვალყურის დევნებით
    Task<List<Group>> GetGroupsForChange(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default);

    //რეჟიმი და გადასვლის დღე (D0). სტრიქონის არქონა ჯგუფების რეჟიმია
    Task<GroupOrdersModeResponse> GetMode(CancellationToken cancellationToken = default);

    //ნომრის წლის შემდეგი ნომერი (D192); მთვლელი ბრძანებასთან ერთად ინახება. ერთდროული შენახვისას მეორე SaveChanges
    //უარყოფილია (LastNumber concurrency token-ია), ამიტომ ნომერი არ მეორდება და წაშლილი ნომერიც თავიდან არ გაიცემა
    Task<OrderNumber> NextNumber(int numberYear, CancellationToken cancellationToken = default);

    //შექმნის ბრძანებით შექმნილი ჯგუფი
    void AddGroup(Group group);

    //მოხსნილი სტრიქონები იშლება. მოსწავლის სტრიქონთან ერთად იშლება მისი ჩანაწერები გაკვეთილებში (LessonsByStudents
    //სტრიქონს Restrict-ით ებმის; შეტანილი მონაცემი მათ D177-ის გამო არ აქვს, D198)
    Task RemoveRows(GroupRemovedRows rows, CancellationToken cancellationToken = default);

    //ჯგუფის რომელიმე გაკვეთილი ხელფასის დათვლაშია (SalaryLinesDetails, D191)
    Task<bool> HasSalary(int groupId, CancellationToken cancellationToken = default);

    //ჯგუფი იშლება გაკვეთილებით, დასწრებით, სტრიქონებით და თავისი ბრძანებებით (D191). group: GetGroupsForChange-იდან
    Task RemoveGroup(Group group, CancellationToken cancellationToken = default);

    //dirty ალმის დასაყენებლად, EF-ის თვალყურის დევნებით
    Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);
}
