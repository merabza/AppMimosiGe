using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator.GenerateGroupsLessons;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.RecountBalances;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class RecountBalancesCommandHandler(
    ICommandHandler<GenerateGroupsLessonsCommand, LessonsGenerationResponse> lessonsGenerator,
    IBalancesRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RecountBalancesCommand, BalancesRecountResponse>
{
    //Access-ის CheckAllStudentsCharges + CheckAllStudentsNextPayDates: ჯერ გაკვეთილები (ნაწილი 09-ის გენერატორი, თითო
    //ჯგუფი ცალკე ტრანზაქციაში), რადგან დარიცხვა მათგან ითვლება და გენერატორი შეცვლილი ჯგუფების კონტრაქტებს dirty-ს
    //ხდის; შემდეგ შემდეგი გადახდის თარიღები
    public async Task<Result<BalancesRecountResponse>> Handle(RecountBalancesCommand command,
        CancellationToken cancellationToken)
    {
        Result<LessonsGenerationResponse> lessons =
            await lessonsGenerator.Handle(new GenerateGroupsLessonsCommand(command.OnlyDirty, false),
                cancellationToken);
        if (lessons.IsFailure)
        {
            return Result.Failure<BalancesRecountResponse>(lessons.Error);
        }

        NextPayDatesRecountResult nextPayDates = await NextPayDatesRecount.Run(repository, unitOfWork,
            command.OnlyDirty, timeProvider.GetLocalNow().Date, cancellationToken);

        return new BalancesRecountResponse(lessons.Value.Groups.Count,
            lessons.Value.Groups.Count(g =>
                g.CreatedLessonsCount + g.UpdatedLessonsCount + g.DeletedLessonsCount + g.AddedStudentsCount +
                g.UpdatedStudentsCount + g.DeletedStudentsCount > 0), lessons.Value.Groups.Sum(g => g.Errors.Count),
            nextPayDates.StudentContractsCount, nextPayDates.ChangedCount);
    }
}
