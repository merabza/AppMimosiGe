using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups.Validation;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Groups.CreateGroup;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CreateGroupCommandHandler(
    IGroupsRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateGroupCommand, int>
{
    public async Task<Result<int>> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        //ვალიდატორი (ValidationDecorator) Request-ის null-ობას უკვე ამოწმებს
        GroupRequest request = command.Request!;

        var group = new Group { GroupCode = request.GroupCode!.Trim() };

        //ახალ ჯგუფს სტრიქონები ჯერ არ აქვს, ამიტომ ყოველი სტრიქონი ახალი (Id 0) უნდა იყოს
        if (!GroupMapper.RowsBelongToGroup(group, request))
        {
            return Result.Failure<int>(GroupErrors.RowNotFound);
        }

        //მასწავლებელს სხვა ჯგუფთან დროების გადაფარვა დღეს ან მომავალში არ ექნება (რეპორტი r18, D178); დღემდე
        //დასრულებული გადაფარვა შენახვას არ აბრკოლებს (D203, დროებით)
        Dictionary<int, TimeOnly> lessonStartTimes = await repository.GetLessonStartTimesById(cancellationToken);
        DateTime today = timeProvider.GetLocalNow().DateTime.Date;
        Error? teacherTimesOverlap = await repository.FindTeacherTimesOverlap(
            GroupMapper.ToTeacherSchedule(group.GrpId, request, lessonStartTimes), today, cancellationToken);
        if (teacherTimesOverlap is not null)
        {
            return Result.Failure<int>(teacherTimesOverlap);
        }

        //მოსწავლეს სხვა ჯგუფთან დროების გადაფარვა დღეს ან მომავალში არ ექნება (რეპორტი r19, D179, D203)
        Error? studentTimesOverlap = await repository.FindStudentTimesOverlap(
            GroupMapper.ToStudentSchedule(group.GrpId, request, lessonStartTimes), today, cancellationToken);
        if (studentTimesOverlap is not null)
        {
            return Result.Failure<int>(studentTimesOverlap);
        }

        Dictionary<int, int> defaultSalarySchemes =
            await repository.GetDefaultSalarySchemes(request.Teachers, cancellationToken);
        GroupMapper.ApplyFields(group, request);
        GroupMapper.ApplyRows(group, request, defaultSalarySchemes);

        //Access: ახალი ჯგუფის DirtyLessons ნაგულისხმევად ჩართულია, მოსწავლეების კონტრაქტებს კი DirtyNextPayDate ერთვება
        group.DirtyLessons = true;
        repository.Add(group);
        await repository.MarkNextPayDatesDirty(request.Students.Select(x => x.StudentContractId), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return group.GrpId;
    }
}
