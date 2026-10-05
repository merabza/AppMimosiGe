using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.AcademicYears;

/// <summary>
///     „ახალი სასწავლო წლის" ოსტატის რეპოზიტორია. რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IAcademicYearsRepository
{
    Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default);

    //წლები დაწყების რიგით, კონტრაქტებისა და ჯგუფების რაოდენობებით
    Task<List<AcademicYearInfoResponse>> GetAcademicYearsInfo(CancellationToken cancellationToken = default);

    void Add(AcademicYear academicYear);

    //წლის ჯგუფები, რომლებიც closeDate-ზე ღიაა (VoidDate ცარიელია ან მის შემდეგაა), ID-ის რიგით
    Task<List<GroupToClose>> GetGroupsToClose(int ayId, DateTime closeDate,
        CancellationToken cancellationToken = default);
}
