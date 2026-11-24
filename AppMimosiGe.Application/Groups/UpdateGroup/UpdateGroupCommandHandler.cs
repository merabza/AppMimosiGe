using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.UpdateGroup;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class UpdateGroupCommandHandler(
    IGroupsRepository repository,
    IGroupOrdersRepository groupOrdersRepository,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateGroupCommand>
{
    public async Task<Result> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        GroupRequest request = command.Request!;

        //ბრძანებების რეჟიმში ჯგუფი მხოლოდ ბრძანებით იცვლება (D183)
        if ((await groupOrdersRepository.GetMode(cancellationToken)).IsOrdersMode)
        {
            return Result.Failure(GroupErrors.GroupsAreManagedByOrders);
        }

        Group? group = await repository.GetForChange(command.GrpId, cancellationToken);
        if (group is null)
        {
            return Result.Failure(GroupErrors.GroupNotFound);
        }

        if (!GroupMapper.RowsBelongToGroup(group, request))
        {
            return Result.Failure(GroupErrors.RowNotFound);
        }

        //დღემდე დაწყებული განრიგის სტრიქონის კვირის დღე ადგილზე არ იცვლება და სტრიქონი არ იშლება (D181): წარსული
        //გაკვეთილები სხვა დღეზე არ გადადის და არ იშლება. სტრიქონი სრულდება, ახალი დღე კი ახალ სტრიქონად ემატება
        DateTime today = timeProvider.GetLocalNow().DateTime.Date;
        if (GroupScheduleWeekDayChangeRule.ChangesStartedRowWeekDay(group, request, today))
        {
            return Result.Failure(GroupErrors.StartedDayTimePlaceWeekDayCannotChange);
        }

        GroupRemovedRows removedRows = GroupMapper.FindRemovedRows(group, request);
        if (GroupScheduleWeekDayChangeRule.RemovesStartedRow(removedRows.DayTimePlaces, today))
        {
            return Result.Failure(GroupErrors.StartedDayTimePlaceCannotBeRemoved);
        }

        //მოსწავლის სტრიქონს, რომელზეც უკვე არის გაკვეთილები, LessonsByStudents Restrict-ით ებმის (Access-ის RI)
        if (removedRows.Students.Count > 0 && await repository.AnyStudentRowIsInUse(
                [.. removedRows.Students.Select(x => x.GbsId)], cancellationToken))
        {
            return Result.Failure(GroupErrors.GroupStudentIsInUse);
        }

        //ცვლილებამდელი მოსწავლეებიც: ამოშლილი ან სხვა კონტრაქტზე გადაყვანილი სტრიქონის კონტრაქტის დარიცხვაც იცვლება
        List<int> affectedStudentContractIds =
        [
            .. group.GroupsByStudents.Select(x => x.StudentContractId),
            .. request.Students.Select(x => x.StudentContractId)
        ];

        Dictionary<int, int> defaultSalarySchemes =
            await repository.GetDefaultSalarySchemes(request.Teachers, cancellationToken);
        Dictionary<int, TimeOnly> lessonStartTimes = await repository.GetLessonStartTimesById(cancellationToken);

        //მასწავლებელს სხვა ჯგუფთან დროების გადაფარვა დღეს ან მომავალში არ ექნება (რეპორტი r18, D178), არც უკვე
        //არსებული. დღემდე დასრულებული გადაფარვა შენახვას არ აბრკოლებს (D203, დროებით). Access ამას მხოლოდ რეპორტით
        //აჩვენებდა
        Error? teacherTimesOverlap = await repository.FindTeacherTimesOverlap(
            GroupMapper.ToTeacherSchedule(group.GrpId, request, lessonStartTimes), today, cancellationToken);
        if (teacherTimesOverlap is not null)
        {
            return Result.Failure(teacherTimesOverlap);
        }

        //მოსწავლეს სხვა ჯგუფთან დროების გადაფარვა დღეს ან მომავალში არ ექნება (რეპორტი r19, D179, D203), არც უკვე
        //არსებული
        Error? studentTimesOverlap = await repository.FindStudentTimesOverlap(
            GroupMapper.ToStudentSchedule(group.GrpId, request, lessonStartTimes), today, cancellationToken);
        if (studentTimesOverlap is not null)
        {
            return Result.Failure(studentTimesOverlap);
        }

        //ცვლილება შეცდომიან გაკვეთილებს არ ქმნის (რეპორტი r13, D177): შეტანილი მონაცემის მქონე გაკვეთილი ან მოსწავლე
        //ზედმეტი არ ხდება. Access ამას არ ამოწმებდა და გენერატორი შეცდომებს 11 და 14 წერდა
        IReadOnlyList<ErroneousLessonStudent> newErrors = await GroupLessonsGeneration.FindNewLessonErrors(
            lessonGeneratorRepository, unitOfWork, group.GrpId, timeProvider.GetLocalNow().DateTime,
            current => GroupMapper.ToLessonsInput(request, current, defaultSalarySchemes, lessonStartTimes),
            cancellationToken);
        if (newErrors.Count > 0)
        {
            return Result.Failure(
                GroupErrors.ChangeCreatesLessonsWithErrors([.. newErrors.Select(e => e.LessonDt).Distinct().Order()]));
        }

        GroupMapper.ApplyFields(group, request);
        GroupMapper.ApplyRows(group, request, defaultSalarySchemes);
        repository.RemoveRows(removedRows);

        //Access-ის FrmGroups.Form_BeforeUpdate და ქვე-ფორმების AfterUpdate/AfterDelConfirm: ჯგუფის ან მისი
        //ნებისმიერი სტრიქონის ცვლილება გაკვეთილების დაზუსტებას და მოსწავლეების შემდეგი გადახდის თარიღის
        //გადათვლას მოითხოვს. სხვა ჯგუფები და კონტრაქტები არ ინიშნება (D56)
        group.DirtyLessons = true;
        await repository.MarkNextPayDatesDirty(affectedStudentContractIds, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
