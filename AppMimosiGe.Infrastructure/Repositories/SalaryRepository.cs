using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Salary;
using AppMimosiGe.Application.Salary.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class SalaryRepository(IMimosiGeDbContext context) : ISalaryRepository
{
    public Task<List<SalaryHeaderRowResponse>> GetHeaders(CancellationToken cancellationToken = default)
    {
        return context.SalaryHeaders.AsNoTracking().OrderBy(h => h.ShChargeDate).ThenBy(h => h.ShId)
            .Select(h => new SalaryHeaderRowResponse(h.ShId, h.ShChargeDate, h.ShTransferDate, h.SalaryLines.Count,
                h.SalaryLines.Sum(l => (decimal?)l.SaAmountNet) ?? 0m)).ToListAsync(cancellationToken);
    }

    //მდგენელები თანამშრომლით; სტრიქონები Access-ის ქვეფორმის რიგით (დარიცხული თანხა კლებადობით, თანამშრომელი);
    //დეტალები თანამშრომლით და ჯგუფით
    public async Task<SalaryHeaderResponse?> GetHeader(int shId, CancellationToken cancellationToken = default)
    {
        SalaryHeader? header = await context.SalaryHeaders.AsNoTracking()
            .SingleOrDefaultAsync(h => h.ShId == shId, cancellationToken);
        if (header is null)
        {
            return null;
        }

        List<SalaryPartResponse> parts = await context.SalaryParts.AsNoTracking().Where(p => p.ShId == shId)
            .Select(p => new
            {
                p.SpId,
                p.TeacherContractId,
                EmployeeName =
                    p.TeacherContract.TeacherHuman.LastName + " " + p.TeacherContract.TeacherHuman.FirstName +
                    " / " + p.TeacherContract.ContractNumber,
                p.SalaryPartTypeId,
                SalaryPartTypeName = p.SalaryPartType == null ? null : p.SalaryPartType.SptName,
                p.SpAmount
            }).OrderBy(p => p.EmployeeName).ThenBy(p => p.SalaryPartTypeId).ThenBy(p => p.SpId)
            .Select(p => new SalaryPartResponse(p.SpId, p.TeacherContractId, p.EmployeeName, p.SalaryPartTypeId,
                p.SalaryPartTypeName, p.SpAmount)).ToListAsync(cancellationToken);

        List<SalaryLineResponse> lines = await context.SalaryLines.AsNoTracking().Where(l => l.ShId == shId)
            .Select(l => new
            {
                Line = l,
                EmployeeName =
                    l.TeacherContract.TeacherHuman.LastName + " " + l.TeacherContract.TeacherHuman.FirstName +
                    " / " + l.TeacherContract.ContractNumber
            }).OrderByDescending(x => x.Line.SaAmountGross).ThenBy(x => x.EmployeeName).ThenBy(x => x.Line.SaId)
            .Select(x => new SalaryLineResponse(x.Line.SaId, x.Line.TeacherContractId, x.EmployeeName,
                x.Line.SaNetAmountRound, x.Line.SaAmountGross, x.Line.SaPension2, x.Line.SaGrossMinusPension,
                x.Line.SaIncomeTax, x.Line.SaGamokvitva, x.Line.SaPension4, x.Line.SaAmountNet, x.Line.SaMonthDate,
                x.Line.RsQuoteTypeId, x.Line.SaIndividualIncomeTax)).ToListAsync(cancellationToken);

        List<SalaryLineDetailResponse> details = await context.SalaryLinesDetails.AsNoTracking()
            .Where(d => d.SalaryLine.ShId == shId).Select(d => new
            {
                d.SadId,
                d.SaId,
                EmployeeName =
                    d.SalaryLine.TeacherContract.TeacherHuman.LastName + " " +
                    d.SalaryLine.TeacherContract.TeacherHuman.FirstName + " / " +
                    d.SalaryLine.TeacherContract.ContractNumber,
                d.GroupId,
                d.Group.GroupCode,
                d.SadHoursCount,
                d.SadAmount,
                d.SadHourCost
            }).OrderBy(d => d.EmployeeName).ThenBy(d => d.GroupCode).ThenBy(d => d.SadId)
            .Select(d => new SalaryLineDetailResponse(d.SadId, d.SaId, d.EmployeeName, d.GroupId, d.GroupCode,
                d.SadHoursCount, d.SadAmount, d.SadHourCost)).ToListAsync(cancellationToken);

        return new SalaryHeaderResponse(header.ShId, header.ShChargeDate, header.ShTransferDate, parts, lines,
            details);
    }

    public Task<SalaryHeader?> GetHeaderForChange(int shId, CancellationToken cancellationToken = default)
    {
        return context.SalaryHeaders.SingleOrDefaultAsync(h => h.ShId == shId, cancellationToken);
    }

    public Task<SalaryHeader?> GetHeaderWithDataForChange(int shId, CancellationToken cancellationToken = default)
    {
        return context.SalaryHeaders.Include(h => h.SalaryParts).Include(h => h.SalaryLines).AsSplitQuery()
            .SingleOrDefaultAsync(h => h.ShId == shId, cancellationToken);
    }

    public Task<bool> HeaderExists(int shId, CancellationToken cancellationToken = default)
    {
        return context.SalaryHeaders.AnyAsync(h => h.ShId == shId, cancellationToken);
    }

    public async Task<bool> HeaderHasData(int shId, CancellationToken cancellationToken = default)
    {
        return await context.SalaryParts.AnyAsync(p => p.ShId == shId, cancellationToken) ||
               await context.SalaryLines.AnyAsync(l => l.ShId == shId, cancellationToken);
    }

    public Task<SalaryPart?> GetPartForChange(int spId, CancellationToken cancellationToken = default)
    {
        return context.SalaryParts.SingleOrDefaultAsync(p => p.SpId == spId, cancellationToken);
    }

    public Task<bool> EmployeeExists(int teacherContractId, CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AnyAsync(x => x.Id == teacherContractId, cancellationToken);
    }

    public Task<SalaryPartTypeLookupResponse?> GetPartType(int sptId, CancellationToken cancellationToken = default)
    {
        return context.SalaryPartTypes.AsNoTracking().Where(x => x.SptId == sptId)
            .Select(x => new SalaryPartTypeLookupResponse(x.SptId, x.SptName, x.SptCountPlaceId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    //Access-ის ჩამოსაშლელი სიის მსგავსად ყველა კონტრაქტი, დასრულებულიც; სახელი უნიკალური ნომრით მთავრდება
    public Task<List<LookupItemResponse>> GetEmployeeLookups(CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AsNoTracking()
            .Select(x => new
            {
                x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<List<SalaryPartTypeLookupResponse>> GetPartTypeLookups(CancellationToken cancellationToken = default)
    {
        return context.SalaryPartTypes.AsNoTracking().OrderBy(x => x.SptId)
            .Select(x => new SalaryPartTypeLookupResponse(x.SptId, x.SptName, x.SptCountPlaceId))
            .ToListAsync(cancellationToken);
    }

    //vSalaryCountBase0shtc: ყველა კონტრაქტი
    public Task<List<SalaryContractData>> GetContracts(CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AsNoTracking()
            .Select(x => new SalaryContractData(x.Id, x.PensionScheme, x.IndEnt, x.NextMonth))
            .ToListAsync(cancellationToken);
    }

    //VR16TSBase1ChargeDates-ის JOIN-ები: მოსწავლის სტრიქონი ჯგუფის მოსწავლის სტრიქონით (იგივე კონტრაქტი) გაკვეთილის
    //ჯგუფს ეკუთვნის. სტატუსს, შემცვლელს და მაქსიმუმს SalaryCalculator ამუშავებს
    public Task<List<SalaryLessonStudentRow>> GetLessonRows(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        return context.LessonsByStudents.AsNoTracking()
            .Where(s => s.GroupByStudent != null && s.GroupByStudent.StudentContractId == s.StudentContractId &&
                        s.GroupByStudent.GroupId == s.Lesson.GroupId && s.Lesson.LessonDt >= from &&
                        s.Lesson.LessonDt < toExclusive)
            .Select(s => new SalaryLessonStudentRow(s.LessonId, s.Lesson.GroupId, s.Lesson.TeacherContractId,
                s.Lesson.SubstituteTeacherContractId, s.Lesson.SalarySchemaId, s.Lesson.LessonDt,
                s.Lesson.LessonStatusId, s.HoursCount)).ToListAsync(cancellationToken);
    }

    public Task<List<DateTime>> GetOperationMonths(CancellationToken cancellationToken = default)
    {
        return context.OperationMonths.AsNoTracking().Select(x => x.MonthDate).ToListAsync(cancellationToken);
    }

    public Task<Dictionary<int, decimal>> GetHourRates(CancellationToken cancellationToken = default)
    {
        return context.TeacherSalarySchemes.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, x => x.HourSalaryNet, cancellationToken);
    }

    public Task<Dictionary<int, int?>> GetPartTypeCountPlaces(CancellationToken cancellationToken = default)
    {
        return context.SalaryPartTypes.AsNoTracking()
            .ToDictionaryAsync(x => x.SptId, x => x.SptCountPlaceId, cancellationToken);
    }

    //Access-ის ExportTransferFile-ის query (GROUP BY-ისა და განაცემის სახის INNER JOIN-ის გარეშე, D107)
    public async Task<List<TransferFileRow>> GetTransferFileRows(int shId,
        CancellationToken cancellationToken = default)
    {
        Dictionary<int, string> monthNames = await context.GeoMonths.AsNoTracking()
            .ToDictionaryAsync(x => x.GmnId, x => x.GmnName, cancellationToken);
        var lines = await context.SalaryLines.AsNoTracking().Where(l => l.ShId == shId).Select(l => new
        {
            l.TeacherContract.BankAccount,
            l.TeacherContract.TeacherHuman.LegalName,
            l.TeacherContract.TeacherHuman.FirstName,
            l.TeacherContract.TeacherHuman.LastName,
            l.TeacherContract.TeacherHuman.PersonalId,
            l.SaAmountNet,
            l.TeacherContract.Description,
            QuoteTypeName = l.TeacherContract.RsQuoteType == null ? null : l.TeacherContract.RsQuoteType.QtName,
            l.SaMonthDate
        }).ToListAsync(cancellationToken);

        return
        [
            .. lines.Select(l => new TransferFileRow(l.BankAccount, l.LegalName, l.FirstName, l.LastName,
                l.PersonalId, l.SaAmountNet, l.Description, l.QuoteTypeName,
                monthNames.GetValueOrDefault(l.SaMonthDate.Month, string.Empty), l.SaMonthDate.Year))
        ];
    }

    public Task<List<DeclarationFileRow>> GetDeclarationFileRows(DateTime from, DateTime toExclusive,
        CancellationToken cancellationToken = default)
    {
        return context.SalaryLines.AsNoTracking()
            .Where(l => l.SalaryHeader.ShTransferDate >= from && l.SalaryHeader.ShTransferDate < toExclusive)
            .Select(l => new DeclarationFileRow(l.SaId, l.TeacherContract.TeacherHuman.PersonalId,
                l.TeacherContract.TeacherHuman.LegalName, l.TeacherContract.TeacherHuman.FirstName,
                l.TeacherContract.TeacherHuman.LastName, l.TeacherContract.TeacherHuman.LegalAddress,
                l.TeacherContract.RsCountry.Code, l.TeacherContract.RsQuoteTypeId, l.SaAmountGross,
                l.SalaryHeader.ShTransferDate)).ToListAsync(cancellationToken);
    }

    public void AddHeader(SalaryHeader header)
    {
        context.SalaryHeaders.Add(header);
    }

    public void RemoveHeader(SalaryHeader header)
    {
        context.SalaryHeaders.Remove(header);
    }

    public void AddPart(SalaryPart part)
    {
        context.SalaryParts.Add(part);
    }

    public void RemovePart(SalaryPart part)
    {
        context.SalaryParts.Remove(part);
    }

    public void RemoveLine(SalaryLine line)
    {
        context.SalaryLines.Remove(line);
    }
}
