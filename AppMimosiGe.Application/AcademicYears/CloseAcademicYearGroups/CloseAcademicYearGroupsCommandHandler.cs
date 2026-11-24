using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Application.GroupOrders.Models;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGe.Application.LessonGenerator.Models;
using AppMimosiGe.Application.Rights;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Requests;
using AppMimosiGeShared.Contracts.V1.Responses;
using BackendCarcass.Application.Identity;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.AcademicYears.CloseAcademicYearGroups;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CloseAcademicYearGroupsCommandHandler(
    IAcademicYearsRepository repository,
    IGroupOrdersRepository groupOrdersRepository,
    ILessonGeneratorRepository lessonGeneratorRepository,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    GroupOrdersSettings groupOrdersSettings,
    TimeProvider timeProvider) : ICommandHandler<CloseAcademicYearGroupsCommand, CloseAcademicYearGroupsResponse>
{
    //თითო ჯგუფი ერთ ტრანზაქციაშია: VoidDate და გაკვეთილების ცვლილებები ერთი SaveChanges-ით (როგორც გენერატორში, თითო
    //ჯგუფი ცალკე scope-ში). უკვე დახურული ჯგუფები აღარ ირჩევა, ამიტომ განმეორებითი გაშვება არაფერს ცვლის
    public async Task<Result<CloseAcademicYearGroupsResponse>> Handle(CloseAcademicYearGroupsCommand command,
        CancellationToken cancellationToken)
    {
        List<AcademicYear> academicYears = await repository.GetAcademicYears(cancellationToken);
        AcademicYear? academicYear = academicYears.SingleOrDefault(ay => ay.AyId == command.AyId);
        if (academicYear is null)
        {
            return AcademicYearErrors.AcademicYearNotFound;
        }

        DateTime closeDate = (command.CloseDate ?? academicYear.FinishDate).Date;
        if (closeDate <= academicYear.StartDate)
        {
            return AcademicYearErrors.CloseDateIsOutOfRange;
        }

        DateTime now = timeProvider.GetLocalNow().DateTime;
        GenerationHorizon horizon = await GroupLessonsGeneration.PrepareOperationMonths(lessonGeneratorRepository,
            unitOfWork, now, command.DryRun, cancellationToken);
        List<GroupToClose> groupsToClose = await repository.GetGroupsToClose(command.AyId, closeDate,
            cancellationToken);

        //ბრძანებების რეჟიმში ჯგუფი ბრძანება 7-ით იხურება (D197)
        GroupOrdersModeResponse mode = await groupOrdersRepository.GetMode(cancellationToken);
        if (mode.IsOrdersMode)
        {
            Result<List<CloseGroupResponse>> closed = await CloseByOrders(groupsToClose,
                new OrdersClosing(academicYear, closeDate, mode.TransitionDate!.Value, now, horizon), command.DryRun,
                cancellationToken);
            if (closed.IsFailure)
            {
                return closed.Error;
            }

            return new CloseAcademicYearGroupsResponse(command.DryRun, command.AyId, closeDate, horizon.HorizonEnd,
                closed.Value);
        }

        List<CloseGroupResponse> groups = [];
        foreach (GroupToClose groupToClose in groupsToClose)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            GroupClosing? closing = null;
            GroupGenerationResult? result = await GroupLessonsGeneration.Run(
                scope.ServiceProvider.GetRequiredService<ILessonGeneratorRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), groupToClose.GrpId, command.DryRun, now,
                input => GroupLessonsPlanner.PlanGroup(input, horizon.HorizonEnd), cancellationToken,
                group => closing = GroupClosing.Apply(group, closeDate));
            //ჯგუფი შეიძლება ამასობაში წაიშალა: მაშინ Run-ი ჯგუფს არ ამზადებს და null-ს აბრუნებს, სხვა დროს ორივე არის
            if (result is null)
            {
                continue;
            }

            groups.Add(ToResponse(groupToClose, closing!, result));
        }

        return new CloseAcademicYearGroupsResponse(command.DryRun, command.AyId, closeDate, horizon.HorizonEnd, groups);
    }

    //D197, D220: ბრძანება 7-ს იღებს თითო ღია ჯგუფი, რომელსაც ბრძანება 0 ან 1 აქვს; დანარჩენი ბრძანებას ვერ მიიღებს
    //(D202) და ღია რჩება. ჯერ ყველა ჯგუფი მოწმდება შენახვის გარეშე: თუ ერთს მაინც ბრძანება ვერ ექმნება, არცერთი არ
    //იხურება. მერე თითო ჯგუფი თავის ტრანზაქციაში იხურება
    private async Task<Result<List<CloseGroupResponse>>> CloseByOrders(List<GroupToClose> groupsToClose,
        OrdersClosing closing, bool dryRun, CancellationToken cancellationToken)
    {
        HashSet<int> groupsWithOrders =
        [
            .. (await groupOrdersRepository.GetGroupLookups(closing.AcademicYear.AyId, cancellationToken))
            .Where(g => g.CanHaveOrders).Select(g => g.GrpId)
        ];
        List<GroupToClose> groups = [.. groupsToClose.Where(g => groupsWithOrders.Contains(g.GrpId))];

        Result<List<CloseGroupResponse>> planned = await CloseEachByOrder(groups, closing, true, cancellationToken);
        if (planned.IsFailure || dryRun)
        {
            return planned;
        }

        return await CloseEachByOrder(groups, closing, false, cancellationToken);
    }

    //ყოველი ჯგუფი ცალკე scope-ში. შემოწმებისას ყველა მიზეზი გროვდება; შენახვისას (თუ ჯგუფი შემოწმების შემდეგ
    //შეიცვალა) დახურვა პირველივე მიზეზზე ჩერდება
    private async Task<Result<List<CloseGroupResponse>>> CloseEachByOrder(List<GroupToClose> groups,
        OrdersClosing closing, bool dryRun, CancellationToken cancellationToken)
    {
        List<CloseGroupResponse> closed = [];
        List<string> failures = [];
        foreach (GroupToClose groupToClose in groups)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            if (await CloseByOrder(scope.ServiceProvider, groupToClose, closing, dryRun, closed,
                    cancellationToken) is not { } error)
            {
                continue;
            }

            failures.Add(groupToClose.GroupCode + " — " + Describe(error));
            if (!dryRun)
            {
                break;
            }
        }

        return failures.Count > 0 ? GroupOrderErrors.GroupsCannotBeClosed(failures) : closed;
    }

    //ჯგუფის დახურვა ბრძანება 7-ით C2-ის გზით (GroupOrderSaveSteps: თარიღი და უფლება, გადათამაშება, წესები და
    //ჩაწერა) და გენერატორი იმავე ტრანზაქციაში, როგორც ჯგუფების რეჟიმში. ბრძანებას დახურული წლის ნომერი აქვს (D197).
    //აბრუნებს მიზეზს, ან null-ს: ჯგუფი დაიხურა (closed-ს დაემატა) ან ამასობაში წაიშალა
    private async Task<Error?> CloseByOrder(IServiceProvider services, GroupToClose groupToClose,
        OrdersClosing closing, bool dryRun, List<CloseGroupResponse> closed, CancellationToken cancellationToken)
    {
        var lessonGenerator = services.GetRequiredService<ILessonGeneratorRepository>();
        var groupUnitOfWork = services.GetRequiredService<IUnitOfWork>();
        var ordersRepository = services.GetRequiredService<IGroupOrdersRepository>();
        var steps = new GroupOrderSaveSteps(ordersRepository, services.GetRequiredService<IGroupsRepository>(),
            services.GetRequiredService<IValidator<GroupRequest>>(), lessonGenerator, groupUnitOfWork,
            services.GetRequiredService<IUserClaimRights>(), timeProvider);

        //დახურვის მონაცემები ბრძანებამდე: ძველი VoidDate, ღია სტრიქონები და წასაშლელი გაკვეთილების დარიცხვა
        Group? group = await lessonGenerator.GetGroupForGeneration(groupToClose.GrpId, !dryRun, cancellationToken);
        if (group is null)
        {
            return null;
        }

        GroupClosing groupClosing = GroupClosing.Apply(group, closing.CloseDate);
        var closeOrder = new GroupOrderData
        {
            Id = GroupOrderSaveSteps.NewOrderId,
            OrderType = EGroupOrderType.CloseGroup,
            OrderDate = closing.CloseDate,
            GroupId = groupToClose.GrpId
        };
        if (await steps.CheckDate(null, closeOrder, cancellationToken) is { } dateError)
        {
            return dateError;
        }

        var order = new GroupOrder { CreatedByUserName = currentUser.Name, CreatedAt = closing.Now };
        ordersRepository.AddOrder(order);
        if (await steps.Save(order, null, closeOrder, closing.TransitionDate, cancellationToken) is { } saveError)
        {
            return saveError;
        }

        if (!dryRun)
        {
            await ordersRepository.Issue(order, closing.AcademicYear.StartDate, closing.Now, groupOrdersSettings,
                cancellationToken);
        }

        GroupGenerationResult? result;
        try
        {
            //შემოწმებისას გენერატორი ჯგუფს ბაზიდან ტვირთავს, ბრძანების გარეშე, ამიტომ დახურვას თვითონ უწერს
            result = await GroupLessonsGeneration.Run(lessonGenerator, groupUnitOfWork, groupToClose.GrpId, dryRun,
                closing.Now, input => GroupLessonsPlanner.PlanGroup(input, closing.Horizon.HorizonEnd),
                cancellationToken, g => g.VoidDate = closing.CloseDate);
        }
        catch (DbUpdateConcurrencyException)
        {
            return GroupOrderErrors.OrderSavedConcurrently;
        }

        if (result is not null)
        {
            closed.Add(ToResponse(groupToClose, groupClosing, result));
        }

        return null;
    }

    private static CloseGroupResponse ToResponse(GroupToClose groupToClose, GroupClosing closing,
        GroupGenerationResult result)
    {
        GroupLessonsGenerationResponse generation = result.Response;
        return new CloseGroupResponse(groupToClose.GrpId, groupToClose.GroupCode, groupToClose.CourseName,
            closing.PreviousVoidDate, closing.OpenTeacherRowsCount, closing.OpenStudentRowsCount,
            closing.OpenScheduleRowsCount,
            closing.DeletedLessonsCharges(generation.Changes
                .Where(c => c.Action == LessonGeneratorMapper.DeleteAction).Select(c => c.LessonId).OfType<int>()),
            generation);
    }

    //ვალიდატორის შეცდომა თავისი ყველა შეცდომის ტექსტით
    private static string Describe(Error error)
    {
        return error is ValidationError validation
            ? string.Join("; ", validation.Errors.Select(e => e.Description))
            : error.Description;
    }

    //ბრძანებებით დახურვის საერთო მონაცემები: წელი (ნომრის წელი მისგანაა), დახურვის და გადასვლის დღეები, დრო და
    //გენერატორის ჰორიზონტი
    private sealed record OrdersClosing(
        AcademicYear AcademicYear,
        DateTime CloseDate,
        DateTime TransitionDate,
        DateTime Now,
        GenerationHorizon Horizon);
}
