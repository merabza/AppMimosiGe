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
///     კონტრაქტების მოსწავლეები ან, სრული გადაანგარიშებისას, ყველა. თითოეულს NextPayDate ეწერება (ვალის გარეშე NULL) და
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
        //მოსწავლის ანგარიში (ნაწილი 20): თარიღი მოსწავლის ყველა კონტრაქტის ოპერაციებით ითვლება და ყველა მათგანს ეწერება.
        //რეპოზიტორია dirty კონტრაქტის მოსწავლის ყველა კონტრაქტს აბრუნებს
        Dictionary<int, int> studentByContract = studentContracts.ToDictionary(sc => sc.ScId, sc => sc.StudentHumanId);
        ILookup<int, BalanceOperation> operations = StudentAccounts.ByAccount(
            BalanceOperations.Build(await repository.GetCharges(scIds, cancellationToken),
                await repository.GetPayments(scIds, cancellationToken)), studentByContract, o => o.StudentContractId);

        int changedCount = 0;
        foreach (StudentContract studentContract in studentContracts)
        {
            List<BalanceOperation> contractOperations = [.. operations[studentContract.StudentHumanId]];
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
