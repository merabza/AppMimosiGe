using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.StudentContracts;
using AppMimosiGe.Application.StudentContracts.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.GetLessonFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetLessonFormLookupsQueryHandler(
    ILessonsRepository repository,
    IStudentContractsRepository studentContractsRepository,
    TimeProvider timeProvider) : IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>
{
    public async Task<Result<LessonFormLookupsResponse>> Handle(GetLessonFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        //DbContext ერთდროულ მოთხოვნებს არ უშვებს, ამიტომ თანმიმდევრულად
        List<LookupItemResponse> groups = await repository.GetGroups(cancellationToken);
        List<LookupItemResponse> teacherContracts = await repository.GetTeacherContracts(cancellationToken);
        List<LookupItemResponse> lessonStatuses = await repository.GetLessonStatuses(cancellationToken);
        //სასწავლო წლები და მიმდინარე წელი იგივეა (და იგივენაირად ლაგდება), რაც კონტრაქტების ფორმაში
        List<AcademicYear> academicYears = await studentContractsRepository.GetAcademicYears(cancellationToken);
        return new LessonFormLookupsResponse(groups, teacherContracts, lessonStatuses,
            CurrentAcademicYear.Find(academicYears, timeProvider.GetLocalNow().Date), [
                .. academicYears.OrderBy(ay => ay.AcademicYearName, StringComparer.Ordinal)
                    .Select(ay => new LookupItemResponse(ay.AyId, ay.AcademicYearName))
            ]);
    }
}
