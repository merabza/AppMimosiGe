using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Groups;

/// <summary>
///     ჯგუფების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია. საერთო ცნობარებს (წლები, საგნები, ზომები,
///     სტატუსები, ხელფასის სქემები) და მათ შემოწმებას მოსწავლეებისა და მასწავლებლების კონტრაქტების რეპოზიტორიები იძლევა
/// </summary>
public interface IGroupsRepository
{
    Task<GroupsRowsDataResponse> GetRowsData(GroupsListQuery query, CancellationToken cancellationToken = default);

    Task<GroupResponse?> GetOne(int grpId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად: ჯგუფი მასწავლებლებით, მოსწავლეებით და განრიგით, EF-ის თვალყურის დევნებით
    Task<Group?> GetForChange(int grpId, CancellationToken cancellationToken = default);

    Task<bool> GroupCodeExists(int academicYearId, string groupCode, int exceptGrpId,
        CancellationToken cancellationToken = default);

    //ჯგუფზე მიბმულია გაკვეთილი, ხელფასის სტრიქონის დეტალი ან მისი მოსწავლის გაკვეთილი
    Task<bool> IsInUse(int grpId, CancellationToken cancellationToken = default);

    //ჯგუფის მოსწავლის რომელიმე სტრიქონზე უკვე არის გაკვეთილი (LessonsByStudents)
    Task<bool> AnyStudentRowIsInUse(IReadOnlyCollection<int> gbsIds, CancellationToken cancellationToken = default);

    Task<bool> TeacherContractExists(int id, CancellationToken cancellationToken = default);
    Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default);
    Task<bool> WeekDayExists(int id, CancellationToken cancellationToken = default);
    Task<bool> LessonStartTimeExists(int lstId, CancellationToken cancellationToken = default);
    Task<bool> RoomExists(int id, CancellationToken cancellationToken = default);

    //მასწავლებლის კონტრაქტის ხელფასის ძირითადი სქემა (SalarySchemaByHours); null, თუ კონტრაქტს სქემა არ აქვს
    Task<int?> GetDefaultSalarySchemeId(int teacherContractId, CancellationToken cancellationToken = default);

    //"გვარი სახელი / ნომერი"-თ დალაგებული, როგორც Access-ის ფორმის ჩამოსაშლელ სიებში
    Task<List<GroupTeacherContractLookupResponse>> GetTeacherContracts(CancellationToken cancellationToken = default);

    Task<List<GroupStudentContractLookupResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default);

    //Access-ის ფორმის რიგით: დღეები ID-ით, დროები ზრდადობით, ოთახები სახელით
    Task<List<LookupItemResponse>> GetWeekDays(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetLessonStartTimes(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetRooms(CancellationToken cancellationToken = default);

    //dirty ალმის დასაყენებლად, EF-ის თვალყურის დევნებით
    Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default);

    void Add(Group group);

    //ჯგუფი იშლება მასწავლებლების, მოსწავლეებისა და განრიგის სტრიქონებთან ერთად (ისინი ჯგუფს Restrict-ით ებმის)
    void Remove(Group group);

    void RemoveRows(GroupRemovedRows rows);
}
