using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Balances.GetDeposits;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class GetDepositsQueryHandler(IBalancesRepository repository, TimeProvider timeProvider)
    : IQueryHandler<GetDepositsQuery, DepositsResponse>
{
    //მონაცემები ბაზიდან იტვირთება, ბალანსები, თარიღები და ფილტრი კი DepositsCalculator-ში ითვლება
    public async Task<Result<DepositsResponse>> Handle(GetDepositsQuery request, CancellationToken cancellationToken)
    {
        if (!TryParseFilter(request.Filter, out EDepositsFilter filter))
        {
            return BalanceErrors.DepositsFilterIsInvalid;
        }

        DateTime today = timeProvider.GetLocalNow().Date;
        List<DepositContractData> contracts =
            await repository.GetDepositContracts(request.AcademicYearId, cancellationToken);
        int[] scIds = [.. contracts.Select(c => c.StudentContractId)];
        List<BalanceOperation> operations = BalanceOperations.Build(
            await repository.GetCharges(scIds, cancellationToken),
            await repository.GetPayments(scIds, cancellationToken));

        DepositsInput input = new(contracts, operations,
            await repository.GetNextLessonDates(scIds, today, cancellationToken),
            await repository.GetCrmMustPayDates(scIds, cancellationToken),
            await repository.GetGroupStudents(scIds, cancellationToken),
            await repository.GetLastOperationMonth(cancellationToken));

        return DepositsCalculator.Build(input, new DepositsParameters(request.Maximum, request.DateTo, filter, today));
    }

    //ცარიელი: ფილტრის გარეშე. სახელით, რეგისტრის მიუხედავად; რიცხვითი მნიშვნელობა არ მიიღება
    private static bool TryParseFilter(string? value, out EDepositsFilter filter)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            filter = EDepositsFilter.None;
            return true;
        }

        return Enum.TryParse(value, true, out filter) && Enum.IsDefined(filter) && !int.TryParse(value, out _);
    }
}
