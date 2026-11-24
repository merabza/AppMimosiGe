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

    //შესაცვლელად ან წასაშლელად: ბრძანება ხაზებით, EF-ის თვალყურის დევნებით
    Task<GroupOrder?> GetOrderForChange(int orderId, CancellationToken cancellationToken = default);

    //ჩასაწერად ან წასაშლელად: ჯგუფები მასწავლებლების, მოსწავლეებისა და განრიგის სტრიქონებით, EF-ის თვალყურის დევნებით
    Task<List<Group>> GetGroupsForChange(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default);

    Task<bool> GroupExists(int grpId, CancellationToken cancellationToken = default);

    //კოდი სასწავლო წელში დაკავებულია (D186): ის აქვს ჯგუფს (exceptGrpId-ის გარდა) ან დაპაუზებულ შექმნის ბრძანებას
    //(exceptOrderId-ის გარდა)
    Task<bool> GroupCodeIsTaken(int academicYearId, string groupCode, int? exceptGrpId, int? exceptOrderId,
        CancellationToken cancellationToken = default);

    //რეჟიმი და გადასვლის დღე (D0). სტრიქონის არქონა ჯგუფების რეჟიმია
    Task<GroupOrdersModeResponse> GetMode(CancellationToken cancellationToken = default);

    //ნომრის წლის შემდეგი ნომერი (D192); მთვლელი ბრძანებასთან ერთად ინახება. ერთდროული შენახვისას მეორე SaveChanges
    //უარყოფილია (LastNumber concurrency token-ია), ამიტომ ნომერი არ მეორდება და წაშლილი ნომერიც თავიდან არ გაიცემა
    Task<OrderNumber> NextNumber(int numberYear, CancellationToken cancellationToken = default);

    //ბრძანების ნომერი (D192); null: ბრძანებას ნომერი არ აქვს (საწყისი ბრძანება)
    Task<OrderNumber?> GetNumber(int orderId, CancellationToken cancellationToken = default);

    void AddOrder(GroupOrder order);

    //ბრძანება იშლება ხაზებით. order: GetOrderForChange-იდან; ხაზებზე მიბმული სტრიქონები უკვე მოხსნილია
    void RemoveOrder(GroupOrder order);

    //ბრძანებიდან ამოღებული ხაზები იშლება; მათზე მიბმული სტრიქონები უკვე მოხსნილია
    void RemoveLines(GroupOrderRemovedLines lines);

    //ჯგუფის ბრძანებების სასწავლო წელი, როცა ჯგუფის წელი საწყის ან შექმნის ბრძანებაში შეიცვალა
    Task SetOrdersAcademicYear(int groupId, int academicYearId, CancellationToken cancellationToken = default);

    //შექმნის ბრძანებით შექმნილი ჯგუფი
    void AddGroup(Group group);

    //მოხსნილი სტრიქონები იშლება. მოსწავლის სტრიქონთან ერთად იშლება მისი ჩანაწერები გაკვეთილებში (LessonsByStudents
    //სტრიქონს Restrict-ით ებმის; შეტანილი მონაცემი მათ D177-ის გამო არ აქვს, D198)
    Task RemoveRows(GroupRemovedRows rows, CancellationToken cancellationToken = default);

    //ჯგუფის რომელიმე გაკვეთილი ხელფასის დათვლაშია (SalaryLinesDetails, D191)
    Task<bool> HasSalary(int groupId, CancellationToken cancellationToken = default);

    //ჯგუფი იშლება გაკვეთილებით, დასწრებით, სტრიქონებით და თავისი ბრძანებებით (D191), keptOrderId-ის გარდა: ხელახლა
    //დაპაუზებული შექმნის ბრძანება რჩება (D213). group: GetGroupsForChange-იდან
    Task RemoveGroup(Group group, int? keptOrderId, CancellationToken cancellationToken = default);

    //dirty ალმის დასაყენებლად, EF-ის თვალყურის დევნებით
    Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    //ბრძანებების სიის ერთი გვერდი: ფილტრი, დათვლა და დალაგება SQL სერვერზე. ბოლოს Id-ით, რომ გვერდებად დაყოფა მდგრადი
    //იყოს
    Task<GroupOrdersRowsDataResponse> GetRowsData(GroupOrdersListQuery query,
        CancellationToken cancellationToken = default);

    //ბრძანება რედაქტირებისთვის, ხაზებით
    Task<GroupOrderResponse?> GetOne(int orderId, CancellationToken cancellationToken = default);

    //ბრძანება ხაზებით ძრავისთვის, EF-ის თვალყურის დევნების გარეშე
    Task<GroupOrderData?> GetOrder(int orderId, CancellationToken cancellationToken = default);

    //ბრძანება ბეჭდვისთვის: ხაზები, ნომერი, გამოცემის მონაცემები და ჯგუფის სახელები
    Task<GroupOrderPrintSource?> GetPrintSource(int orderId, CancellationToken cancellationToken = default);

    //ბეჭდვის სახელები: მასწავლებლებისა და მოსწავლეების „გვარი სახელი" მოთხოვნილი კონტრაქტებით, ხელფასის სქემები,
    //კვირის დღეები, დაწყების დროები და ოთახები
    Task<GroupOrderPrintNames> GetPrintNames(IReadOnlyCollection<int> teacherContractIds,
        IReadOnlyCollection<int> studentContractIds, CancellationToken cancellationToken = default);

    //ჯგუფების ახლანდელი მდგომარეობა ბაზის სტრიქონებით, ჯგუფის Id-ით. სტრიქონის LineId ბაზის სტრიქონის Id-ია, როგორც
    //ვიზარდი 1-ის შესავალში (GroupOrdersSwitchPlanner); ჯგუფი, რომელიც არ არის, შედეგში არ არის
    Task<Dictionary<int, GroupState>> GetGroupStates(IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken = default);

    //სასწავლო წლის ჯგუფები ბრძანების ფორმისა და სიის ფილტრისთვის, კოდის რიგით. CanHaveOrders: D202
    Task<List<GroupOrderGroupLookupResponse>> GetGroupLookups(int academicYearId,
        CancellationToken cancellationToken = default);

    //სასწავლო წლის დაკავებული კოდები (D186): ჯგუფებისა (exceptGrpId-ის გარდა) და დაპაუზებული შექმნის ბრძანებებისა
    //(exceptOrderId-ის გარდა), როგორც GroupCodeIsTaken-ში
    Task<List<string>> GetTakenGroupCodes(int academicYearId, int? exceptGrpId, int? exceptOrderId,
        CancellationToken cancellationToken = default);

    //მოსწავლის სტატუსის Rate, კოდის კლასისთვის (D186); null, თუ სტატუსი არ არის
    Task<int?> GetStudentStatusRate(int studentStatusId, CancellationToken cancellationToken = default);
}
