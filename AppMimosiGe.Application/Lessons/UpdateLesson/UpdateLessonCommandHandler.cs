using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.UpdateLesson;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateLessonCommandHandler(ILessonsRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateLessonCommand>
{
    public async Task<Result> Handle(UpdateLessonCommand command, CancellationToken cancellationToken)
    {
        LessonRequest request = command.Request!;

        Lesson? lesson = await repository.GetForChange(command.LessonId, cancellationToken);
        if (lesson is null)
        {
            return Result.Failure(LessonErrors.LessonNotFound);
        }

        Dictionary<int, LessonByStudent> rows = lesson.LessonsByStudents.ToDictionary(x => x.Id);
        if (request.Students.Any(x => !rows.ContainsKey(x.Id)))
        {
            return Result.Failure(LessonErrors.StudentRowNotFound);
        }

        LessonMapper.ApplyFields(lesson, request);
        foreach (LessonStudentRequest row in request.Students)
        {
            LessonMapper.ApplyStudentFields(rows[row.Id], row);
        }

        //Access-ის FrmLessons.Form_AfterUpdate და ქვე-ფორმის AfterUpdate: ჯგუფის მოსწავლეების შემდეგი გადახდის
        //თარიღი გადასათვლელია (SetStudentNextPayDateDirtyForGroup). FrmLessons-ში მას ჯგუფის ნაცვლად გაკვეთილის ID
        //გადაეცემოდა (Access-ის ბაგი, D70); აქ ჯგუფია. SetDirtyByGroup-ის ალამი GroupsByStudents.DirtyCharges D9-ით
        //წაიშალა. ყველაფერი იმავე SaveChanges-ით (ერთ ტრანზაქციაში) ინახება
        foreach (StudentContract studentContract in await repository.GetStudentContractsForChange(lesson.GroupId,
                     lesson.Id, cancellationToken))
        {
            studentContract.DirtyNextPayDate = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
