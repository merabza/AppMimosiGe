using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.Errors;
using AppMimosiGeShared.Contracts.V1.Responses;
using MimosiGeCore.Domain.Models;
using SystemTools.Application.Abstractions.Messaging;
using SystemTools.Domain.Abstractions;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.Salary.CountSalary;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class CountSalaryCommandHandler(ISalaryRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CountSalaryCommand, SalaryCountResponse>
{
    //Access-ის CountSalary: უწყისის სტრიქონები (დეტალებით) და ტიპი 1-ის მდგენელები იშლება, ხელით შეტანილი
    //მდგენელები რჩება, შემდეგ ყველაფერი თავიდან ითვლება. ყველაფერი ერთი SaveChanges-ით (ერთ ტრანზაქციაში).
    //დადასტურებას ("Clear old data?") UI ითხოვს
    public async Task<Result<SalaryCountResponse>> Handle(CountSalaryCommand command,
        CancellationToken cancellationToken)
    {
        SalaryHeader? header = await repository.GetHeaderWithDataForChange(command.ShId, cancellationToken);
        if (header is null)
        {
            return SalaryErrors.SalaryHeaderNotFound;
        }

        //ჩვეულებრივ კონტრაქტს წინა თვის გაკვეთილები ეკუთვნის, NextMonth-ისას იმავე თვისა
        DateTime from = SalaryCalculator.LineMonthDate(header.ShChargeDate);
        var input = new SalaryCalculationInput(header.ShChargeDate, await repository.GetContracts(cancellationToken),
            await repository.GetLessonRows(from, from.AddMonths(2), cancellationToken),
            (await repository.GetOperationMonths(cancellationToken)).ToHashSet(),
            await repository.GetHourRates(cancellationToken),
            await repository.GetPartTypeCountPlaces(cancellationToken),
            [
                .. header.SalaryParts.Where(x => x.SalaryPartTypeId != SalaryCalculator.LessonSalaryPartTypeId)
                    .Select(x => new SalaryPartData(x.TeacherContractId, x.SalaryPartTypeId, x.SpAmount))
            ]);
        SalaryCalculationResult result = SalaryCalculator.Calculate(input);

        //დეტალებს ბაზა შლის კასკადით
        foreach (SalaryLine line in header.SalaryLines.ToList())
        {
            repository.RemoveLine(line);
        }

        foreach (SalaryPart part in header.SalaryParts
                     .Where(x => x.SalaryPartTypeId == SalaryCalculator.LessonSalaryPartTypeId).ToList())
        {
            repository.RemovePart(part);
        }

        foreach (CalculatedSalaryPart part in result.LessonParts)
        {
            header.SalaryParts.Add(new SalaryPart
            {
                TeacherContractId = part.TeacherContractId,
                SalaryPartTypeId = SalaryCalculator.LessonSalaryPartTypeId,
                SpAmount = part.Amount
            });
        }

        foreach (CalculatedSalaryLine line in result.Lines)
        {
            header.SalaryLines.Add(ToSalaryLine(line));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SalaryCountResponse(result.LessonParts.Count, result.Lines.Count,
            result.Lines.Sum(x => x.Details.Count));
    }

    private static SalaryLine ToSalaryLine(CalculatedSalaryLine line)
    {
        return new SalaryLine
        {
            TeacherContractId = line.TeacherContractId,
            SaNetAmountRound = line.NetAmountRound,
            SaAmountGross = line.AmountGross,
            SaPension2 = line.Pension2,
            SaGrossMinusPension = line.GrossMinusPension,
            SaIncomeTax = line.IncomeTax,
            SaGamokvitva = line.Gamokvitva,
            SaPension4 = line.Pension4,
            SaAmountNet = line.AmountNet,
            SaMonthDate = line.MonthDate,
            RsQuoteTypeId = line.RsQuoteTypeId,
            SaIndividualIncomeTax = line.IndividualIncomeTax,
            SalaryLinesDetails = new List<SalaryLineDetail>(line.Details.Select(x => new SalaryLineDetail
            {
                GroupId = x.GroupId, SadAmount = x.Amount, SadHoursCount = x.HoursCount, SadHourCost = x.HourCost
            }))
        };
    }
}
