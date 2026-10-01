using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.Models;
using MimosiGeCore.Domain.Models;
using SystemTools.Domain.Abstractions;

namespace AppMimosiGe.Application.Balances;

/// <summary>
///     კონტრაქტების შემდეგი გადახდის თარიღის გადათვლა (Access-ის CheckAllStudentsNextPayDates): მხოლოდ dirty
///     კონტრაქტები ან, სრული გადაანგარიშებისას, ყველა. თითოეულს NextPayDate ეწერება (ვალის გარეშე NULL) და
///     DirtyNextPayDate ექრება; ყველაფერი ერთი SaveChanges-ით
/// </summary>
public static class NextPayDatesRecount
{
    public static async Task<NextPayDatesRecountResult> Run(IBalancesRepository repository, IUnitOfWork unitOfWork,
        bool onlyDirty, DateTime today, CancellationToken cancellationToken)
    {
        List<StudentContract> studentContracts =
            await repository.GetStudentContractsForRecount(onlyDirty, cancellationToken);
        if (studentContracts.Count == 0)
        {
            return new NextPayDatesRecountResult(0, 0);
        }

        int[] scIds = [.. studentContracts.Select(sc => sc.ScId)];
        ILookup<int, BalanceOperation> operations = BalanceOperations
            .Build(await repository.GetCharges(scIds, cancellationToken),
                await repository.GetPayments(scIds, cancellationToken)).ToLookup(o => o.StudentContractId);

        int changedCount = 0;
        foreach (StudentContract studentContract in studentContracts)
        {
            List<BalanceOperation> contractOperations = [.. operations[studentContract.ScId]];
            DateTime? nextPayDate = NextPayDateCalculator.Calculate(contractOperations,
                NextPayDateCalculator.LastPayDate(contractOperations, today));
            if (studentContract.NextPayDate != nextPayDate)
            {
                changedCount++;
            }

            studentContract.NextPayDate = nextPayDate;
            studentContract.DirtyNextPayDate = false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new NextPayDatesRecountResult(studentContracts.Count, changedCount);
    }
}

/// <summary>
///     გადათვლილი კონტრაქტები და მათგან, რომელთა შემდეგი გადახდის თარიღი შეიცვალა
/// </summary>
public sealed record NextPayDatesRecountResult(int StudentContractsCount, int ChangedCount);
