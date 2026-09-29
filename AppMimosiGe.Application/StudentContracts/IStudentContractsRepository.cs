using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.StudentContracts;

/// <summary>
///     მოსწავლეების კონტრაქტების რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IStudentContractsRepository
{
    Task<StudentContractsRowsDataResponse> GetRowsData(StudentContractsListQuery query,
        CancellationToken cancellationToken = default);

    Task<StudentContractResponse?> GetOne(int scId, CancellationToken cancellationToken = default);

    //შესაცვლელად ან წასაშლელად: კონტრაქტი დეტალებით, EF-ის თვალყურის დევნებით
    Task<StudentContract?> GetForChange(int scId, CancellationToken cancellationToken = default);

    Task<bool> ContractNumberExists(int academicYearId, string contractNumber, int exceptScId,
        CancellationToken cancellationToken = default);

    //კონტრაქტზე მიბმულია ჯგუფი, გაკვეთილი, გადახდა ან CRM ზარი
    Task<bool> IsInUse(int scId, CancellationToken cancellationToken = default);

    Task<bool> HumanExists(int humId, CancellationToken cancellationToken = default);
    Task<bool> AcademicYearExists(int ayId, CancellationToken cancellationToken = default);
    Task<bool> StudentStatusExists(int studentStatusId, CancellationToken cancellationToken = default);
    Task<bool> CourseExists(int crsId, CancellationToken cancellationToken = default);
    Task<bool> GroupSizeExists(int grsId, CancellationToken cancellationToken = default);

    Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default);

    //სტატუსები Rate-ით დალაგებული, როგორც Access-ის ფორმაში
    Task<List<LookupItemResponse>> GetStudentStatuses(CancellationToken cancellationToken = default);
    Task<List<LookupItemResponse>> GetCourses(CancellationToken cancellationToken = default);

    //სახელი "ზომა-დასახელება", როგორც Access-ის ფორმაში
    Task<List<LookupItemResponse>> GetGroupSizes(CancellationToken cancellationToken = default);

    Task<List<LookupItemResponse>> SearchHumans(string search, int maxCount,
        CancellationToken cancellationToken = default);

    void Add(StudentContract studentContract);
    void Remove(StudentContract studentContract);
}
