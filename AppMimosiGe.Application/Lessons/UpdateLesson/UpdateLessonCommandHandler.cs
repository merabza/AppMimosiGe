using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Lessons.UpdateLesson;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateLessonCommandHandler(
    ILessonsRepository repository,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateLessonCommand>
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

        //გაუქმებული გაკვეთილი დამსწრე მოსწავლით ან აღდგენის თარიღით არ ინახება (რეპორტი r12). დასწრება მოწმდება
        //შენახვის შემდგომი მდგომარეობით: მოთხოვნიდან გამოტოვებული სტრიქონი თავის მნიშვნელობას ინარჩუნებს
        if (!LessonVoidStatusRule.AllowsPresentStudentsAndRecoverDate(request.LessonStatusId))
        {
            if (request.RecoverDate is not null)
            {
                return Result.Failure(LessonErrors.NotHeldLessonCannotHaveRecoverDate);
            }

            Dictionary<int, bool> requestPresent = request.Students.ToDictionary(x => x.Id, x => x.Present);
            if (rows.Values.Any(row => requestPresent.GetValueOrDefault(row.Id, row.Present)))
            {
                return Result.Failure(LessonErrors.NotHeldLessonCannotHavePresentStudents);
            }
        }

        //ჟურნალი მასწავლებლის დროების ახალ გადაფარვას არ ქმნის (D178): გაკვეთილის ჩამტარებელს (შემცვლელს, თუ არის)
        //იმავე დროს სხვა ჩასატარებელი გაკვეთილი არ ექნება
        Error? teacherOverlap = await FindNewTeacherOverlap(lesson, request, cancellationToken);
        if (teacherOverlap is not null)
        {
            return Result.Failure(teacherOverlap);
        }

        //ჟურნალი შეცდომიან გაკვეთილს არ ქმნის (რეპორტი r13, D177): ჯგუფის შეცვლის შემდეგ ზედმეტ გაკვეთილს ან
        //მოსწავლეს, რომელიც გენერატორს ჯერ არ წაუშლია, შეტანილი მონაცემი არ ემატება
        IReadOnlyList<ErroneousLessonStudent> newErrors = await GroupLessonsGeneration.FindNewLessonErrors(
            lessonGeneratorRepository, unitOfWork, lesson.GroupId, timeProvider.GetLocalNow().DateTime,
            current => LessonMapper.ToLessonsInput(current, lesson.Id, request), cancellationToken);
        if (newErrors.Count > 0)
        {
            return Result.Failure(await LessonWithErrorsError(newErrors, cancellationToken));
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

    //შენახვის შემდეგ გაკვეთილის ჩამტარებლის სხვა ჩასატარებელი გაკვეთილები, რომლებიც მის დროს ფარავს
    //(TeacherLessonOverlapsRule). ჩამტარებელი და დრო თუ არ იცვლება, არაფერი მოწმდება: უკვე შენახული გადაფარვა სხვა
    //ცვლილებას არ აბრკოლებს. ახალი დრო (აღდგენის თარიღი) სხვა დღეა, ამიტომ იქ ყოველი გადაფარვა ახალია
    private async Task<Error?> FindNewTeacherOverlap(Lesson lesson, LessonRequest request,
        CancellationToken cancellationToken)
    {
        float hours = lesson.LessonsByStudents.Select(x => x.HoursCount).DefaultIfEmpty().Max();
        LessonTeacherTime? after = TeacherLessonOverlapsRule.TeacherTime(request.LessonStatusId,
            lesson.TeacherContractId, request.SubstituteTeacherContractId, lesson.LessonDt, request.RecoverDate?.Date,
            hours);
        if (after is null || after == TeacherLessonOverlapsRule.TeacherTime(lesson.LessonStatusId,
                lesson.TeacherContractId, lesson.SubstituteTeacherContractId, lesson.LessonDt, lesson.RecoverDate,
                hours))
        {
            return null;
        }

        (DateTime fromDay, DateTime toDay) = TeacherLessonOverlapsRule.SearchDays(after);
        IReadOnlyList<TeacherLesson> overlapping = TeacherLessonOverlapsRule.Overlapping(after,
            await repository.GetTeacherLessons(after.TeacherContractId, fromDay, toDay, lesson.Id, cancellationToken));
        if (overlapping.Count == 0)
        {
            return null;
        }

        Dictionary<int, string> teacherNames =
            (await repository.GetTeacherContracts(cancellationToken)).ToDictionary(x => x.Id, x => x.Name);
        return LessonErrors.TeacherHasAnotherLessonAtThisTime(teacherNames[after.TeacherContractId],
            overlapping.Select(x => x.Describe()));
    }

    //ზედმეტი გაკვეთილი (შეცდომა 11) ან ზედმეტი მოსწავლეები სახელებით (შეცდომა 14). ახალი შეცდომები მხოლოდ ამ
    //გაკვეთილისაა და ერთი სახისაა: ზედმეტ გაკვეთილში მოსწავლეები ცალკე არ მოწმდება
    private async Task<Error> LessonWithErrorsError(IReadOnlyList<ErroneousLessonStudent> newErrors,
        CancellationToken cancellationToken)
    {
        if (newErrors[0].ErrorCode == LessonGeneratorErrorCodes.ExtraLessonHasEnteredData)
        {
            return LessonErrors.ExtraLessonCannotHaveEnteredData;
        }

        List<int> studentContractIds = [.. newErrors.Select(e => e.StudentContractId).Distinct()];
        Dictionary<int, string> names = await repository.GetStudentNames(studentContractIds, cancellationToken);
        return LessonErrors.ExtraStudentsCannotHaveEnteredData(studentContractIds.Select(id => names[id])
            .Order(StringComparer.Ordinal));
    }
}
