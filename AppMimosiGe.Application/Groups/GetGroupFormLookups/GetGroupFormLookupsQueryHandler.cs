using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGe.Application.TeacherContracts;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.GetGroupFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetGroupFormLookupsQueryHandler(
    IGroupsRepository repository,
    IStudentContractsRepository studentContractsRepository,
    ITeacherContractsRepository teacherContractsRepository,
    TimeProvider timeProvider) : IQueryHandler<GetGroupFormLookupsQuery, GroupFormLookupsResponse>
{
    //წლები, საგნები, ზომები, სტატუსები და სქემები იგივეა (და იგივენაირად ლაგდება), რაც კონტრაქტების ფორმებში
    public async Task<Result<GroupFormLookupsResponse>> Handle(GetGroupFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await studentContractsRepository.GetAcademicYears(cancellationToken);
        DateTime today = timeProvider.GetLocalNow().Date;

        return new GroupFormLookupsResponse(CurrentAcademicYear.Find(academicYears, today), [
                .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                    .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
            ], await studentContractsRepository.GetCourses(cancellationToken),
            await studentContractsRepository.GetGroupSizes(cancellationToken),
            await studentContractsRepository.GetStudentStatuses(cancellationToken),
            await repository.GetTeacherContracts(cancellationToken),
            await teacherContractsRepository.GetSalarySchemes(cancellationToken),
            await repository.GetWeekDays(cancellationToken), await repository.GetLessonStartTimes(cancellationToken),
            await repository.GetRooms(cancellationToken));
    }
}
