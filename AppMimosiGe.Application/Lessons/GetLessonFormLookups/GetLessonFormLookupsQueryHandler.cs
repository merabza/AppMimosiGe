using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.GetLessonFormLookups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetLessonFormLookupsQueryHandler(ILessonsRepository repository)
    : IQueryHandler<GetLessonFormLookupsQuery, LessonFormLookupsResponse>
{
    public async Task<Result<LessonFormLookupsResponse>> Handle(GetLessonFormLookupsQuery request,
        CancellationToken cancellationToken)
    {
        //DbContext ერთდროულ მოთხოვნებს არ უშვებს, ამიტომ თანმიმდევრულად
        List<LookupItemResponse> groups = await repository.GetGroups(cancellationToken);
        List<LookupItemResponse> teacherContracts = await repository.GetTeacherContracts(cancellationToken);
        List<LookupItemResponse> lessonStatuses = await repository.GetLessonStatuses(cancellationToken);
        return new LessonFormLookupsResponse(groups, teacherContracts, lessonStatuses);
    }
}
