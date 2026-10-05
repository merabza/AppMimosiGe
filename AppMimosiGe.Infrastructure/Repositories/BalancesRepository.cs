using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Balances;
using AppMimosiGe.Application.Balances.Models;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class BalancesRepository(IMimosiGeDbContext context) : IBalancesRepository
{
    //გაკვეთილის სტატუსი "გაუქმდა": ასეთი გაკვეთილი არ ირიცხება და შემდეგ გაკვეთილად არ ითვლება
    private const int CancelledLessonStatusId = 2;

    //Access-ის vFrmChargesAndPayments-ის join-ები: მოსწავლის სტრიქონი ჯგუფში (GroupsByStudents) LessonsByStudents-ს
    //სტრიქონითაც და კონტრაქტითაც ებმის, ჯგუფი კი გაკვეთილისაც უნდა იყოს. ტარიფი ამ სტრიქონისაა
    public Task<List<ChargeData>> GetCharges(IReadOnlyCollection<int>? scIds,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LessonByStudent> lessonsByStudents = context.LessonsByStudents.AsNoTracking();
        if (scIds is not null)
        {
            lessonsByStudents = lessonsByStudents.Where(x => scIds.Contains(x.StudentContractId));
        }

        return lessonsByStudents
            .Where(x => x.GroupByStudent != null && x.GroupByStudent.StudentContractId == x.StudentContractId &&
                        x.GroupByStudent.GroupId == x.Lesson.GroupId &&
                        x.Lesson.LessonStatusId != CancelledLessonStatusId).Select(x => new ChargeData(x.Id,
                x.StudentContractId, x.Lesson.LessonDt, x.GroupByStudent!.Group.Course.CourseName,
                x.GroupByStudent.FourWeekFee, x.GroupByStudent.FourWeekHours, x.HoursCount))
            .ToListAsync(cancellationToken);
    }

    public Task<List<PaymentData>> GetPayments(IReadOnlyCollection<int>? scIds,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Payment> payments = context.Payments.AsNoTracking();
        if (scIds is not null)
        {
            payments = payments.Where(x => scIds.Contains(x.StudentContractId));
        }

        return payments.Select(x => new PaymentData(x.Id, x.StudentContractId, x.PayDate, x.Document, x.Amount))
            .ToListAsync(cancellationToken);
    }

    public Task<Dictionary<int, string>> GetStudentContractNames(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => scIds.Contains(x.ScId))
            .Select(x => new
            {
                x.ScId, Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " / " + x.ContractNumber
            }).ToDictionaryAsync(x => x.ScId, x => x.Name, cancellationToken);
    }

    public Task<List<DepositContractData>> GetDepositContracts(int? academicYearId,
        CancellationToken cancellationToken = default)
    {
        IQueryable<StudentContract> studentContracts = context.StudentContracts.AsNoTracking();
        if (academicYearId is { } ayId)
        {
            studentContracts = studentContracts.Where(x => x.AcademicYearId == ayId);
        }

        return studentContracts.Select(x => new DepositContractData(x.ScId, x.AcademicYearId,
            x.StudentHuman.LastName + " " + x.StudentHuman.FirstName, x.ContractNumber, x.StudentHuman.PhoneNumber,
            x.PayerHuman.LastName + " " + x.PayerHuman.FirstName, x.PayerHuman.PhoneNumber, x.DesiredMonthlyPaymentDay,
            x.NextPayDate, x.StudentHumanId, x.AcademicYear.StartDate)).ToListAsync(cancellationToken);
    }

    public Task<Dictionary<int, int>> GetStudentAccountContracts(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        IQueryable<int> studentHumanIds = context.StudentContracts.Where(x => scIds.Contains(x.ScId))
            .Select(x => x.StudentHumanId);
        return context.StudentContracts.AsNoTracking().Where(x => studentHumanIds.Contains(x.StudentHumanId))
            .ToDictionaryAsync(x => x.ScId, x => x.StudentHumanId, cancellationToken);
    }

    //კონტრაქტის ნებისმიერი LessonsByStudents-ის სტრიქონის გაკვეთილი (ჯგუფის სტრიქონის შემოწმების გარეშე, როგორც Access-ში)
    public Task<Dictionary<int, DateTime>> GetNextLessonDates(IReadOnlyCollection<int> scIds, DateTime today,
        CancellationToken cancellationToken = default)
    {
        //ცხადი join: ნავიგაციით დაჯგუფებას EF კორელირებულ ქვე-მოთხოვნად თარგმნიდა
        return context.LessonsByStudents.AsNoTracking().Where(x => scIds.Contains(x.StudentContractId))
            .Join(context.Lessons.Where(l => l.LessonDt >= today && l.LessonStatusId != CancelledLessonStatusId),
                x => x.LessonId, l => l.Id, (x, l) => new { x.StudentContractId, l.LessonDt })
            .GroupBy(x => x.StudentContractId, x => x.LessonDt)
            .Select(g => new { StudentContractId = g.Key, NextLessonDate = g.Min() })
            .ToDictionaryAsync(x => x.StudentContractId, x => x.NextLessonDate, cancellationToken);
    }

    public Task<List<CrmMustPayDateData>> GetCrmMustPayDates(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.CrmCalls.AsNoTracking().Where(x => x.MustPayDate != null && scIds.Contains(x.StudentContractId))
            .Select(x => new CrmMustPayDateData(x.CcId, x.StudentContractId, x.CallDate, x.MustPayDate!.Value))
            .ToListAsync(cancellationToken);
    }

    public Task<List<DepositGroupStudentData>> GetGroupStudents(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.GroupsByStudents.AsNoTracking().Where(x => scIds.Contains(x.StudentContractId))
            .Select(x => new DepositGroupStudentData(x.StudentContractId, x.FourWeekFee, x.EndDate, x.Group.VoidDate))
            .ToListAsync(cancellationToken);
    }

    public Task<DateTime?> GetLastOperationMonth(CancellationToken cancellationToken = default)
    {
        return context.OperationMonths.MaxAsync(x => (DateTime?)x.MonthDate, cancellationToken);
    }

    public Task<List<StudentContract>> GetStudentContractsForRecount(bool onlyDirty,
        CancellationToken cancellationToken = default)
    {
        //მოსწავლის ანგარიში მისი ყველა კონტრაქტია, ამიტომ dirty კონტრაქტის მოსწავლის ყველა კონტრაქტი გადაითვლება
        return context.StudentContracts
            .Where(x => !onlyDirty || context.StudentContracts.Any(d =>
                d.DirtyNextPayDate && d.StudentHumanId == x.StudentHumanId)).OrderBy(x => x.ScId)
            .ToListAsync(cancellationToken);
    }
}
