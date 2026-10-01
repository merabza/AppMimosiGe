using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Lessons;
using AppMimosiGe.Application.Lessons.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class LessonsRepository(IMimosiGeDbContext context) : ILessonsRepository
{
    //"არ გაუქმებულა": შეუვსებლად ითვლება მხოლოდ ასეთი გაკვეთილი, გაუქმებულზე დასწრება არც არის მოსალოდნელი
    private const int HeldLessonStatusId = 1;

    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<LessonsRowsDataResponse> GetRowsData(LessonsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LessonRowData> rows = ApplyFilter(context.Lessons.AsNoTracking(), query).Select(l =>
            new LessonRowData
            {
                LessonId = l.Id,
                LessonDt = l.LessonDt,
                GrpId = l.GroupId,
                GroupCode = l.Group.GroupCode,
                CourseName = l.Group.Course.CourseName,
                TeacherName =
                    l.TeacherContract.TeacherHuman.LastName + " " + l.TeacherContract.TeacherHuman.FirstName,
                SubstituteTeacherName =
                    l.SubstituteTeacherContract == null
                        ? null
                        : l.SubstituteTeacherContract.TeacherHuman.LastName + " " +
                          l.SubstituteTeacherContract.TeacherHuman.FirstName,
                LessonStatusId = l.LessonStatusId,
                LessonStatusName = l.LessonStatus.StatusName,
                StudentsCount = l.LessonsByStudents.Count,
                PresentCount = l.LessonsByStudents.Count(s => s.Present)
            });

        int count = await rows.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        List<LessonRowData> page = await ApplySort(rows, query.SortFields).Skip(offset).Take(query.RowsCount)
            .ToListAsync(cancellationToken);

        return new LessonsRowsDataResponse(count, offset, [
            .. page.Select(r => new LessonRowResponse(r.LessonId, r.LessonDt, r.GrpId, r.GroupCode, r.CourseName,
                r.TeacherName, r.SubstituteTeacherName, r.LessonStatusId, r.LessonStatusName, r.StudentsCount,
                r.PresentCount))
        ]);
    }

    public Task<LessonResponse?> GetOne(int lessonId, CancellationToken cancellationToken = default)
    {
        IQueryable<Lesson> lessons = context.Lessons.AsNoTracking();
        return lessons.Where(l => l.Id == lessonId).Select(l => new LessonResponse(l.Id, l.GroupId, l.Group.GroupCode,
            l.Group.Course.CourseName, l.TeacherContractId,
            l.TeacherContract.TeacherHuman.LastName + " " + l.TeacherContract.TeacherHuman.FirstName + " / " +
            l.TeacherContract.ContractNumber, l.LessonDt, l.SalaryScheme.SchemaName, l.FourWeekHours, l.TeoMinDate,
            l.TeoMaxDate, l.LessonStatusId, l.SubstituteTeacherContractId, l.TeacherLateMinutes, l.RecoverDate, l.Note,
            lessons.Where(p => p.GroupId == l.GroupId && p.LessonDt < l.LessonDt).OrderByDescending(p => p.LessonDt)
                .Select(p => (int?)p.Id).FirstOrDefault(),
            lessons.Where(n => n.GroupId == l.GroupId && n.LessonDt > l.LessonDt).OrderBy(n => n.LessonDt)
                .Select(n => (int?)n.Id).FirstOrDefault(),
            //Access-ის ქვე-ფორმის დალაგება: მოსწავლის სახელი
            l.LessonsByStudents
                .OrderBy(s => s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName)
                .ThenBy(s => s.Id).Select(s => new LessonStudentResponse(s.Id, s.StudentContractId,
                    s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName,
                    s.HoursCount, s.Present, s.Theme, s.Rate, s.TeacherComment, s.StudentComment, s.StudentLateMinutes))
                .ToList())).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Lesson?> GetForChange(int lessonId, CancellationToken cancellationToken = default)
    {
        return context.Lessons.Include(l => l.LessonsByStudents)
            .SingleOrDefaultAsync(l => l.Id == lessonId, cancellationToken);
    }

    public Task<bool> LessonStatusExists(int id, CancellationToken cancellationToken = default)
    {
        return context.LessonStatuses.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> TeacherContractExists(int id, CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetGroups(CancellationToken cancellationToken = default)
    {
        return context.Groups.AsNoTracking().OrderByDescending(x => x.AcademicYear.StartDate).ThenBy(x => x.GroupCode)
            .ThenBy(x => x.GrpId)
            .Select(x => new LookupItemResponse(x.GrpId, x.GroupCode + " / " + x.AcademicYear.AcademicYearName))
            .ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetTeacherContracts(CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AsNoTracking()
            .Select(x => new
            {
                x.Id, Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber
            }).OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetLessonStatuses(CancellationToken cancellationToken = default)
    {
        return context.LessonStatuses.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new LookupItemResponse(x.Id, x.StatusName)).ToListAsync(cancellationToken);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(int grpId, int lessonId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x =>
                x.GroupsByStudents.Any(g => g.GroupId == grpId) || x.LessonsByStudents.Any(s => s.LessonId == lessonId))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Lesson> ApplyFilter(IQueryable<Lesson> lessons, LessonsListQuery query)
    {
        if (query.GrpId is { } grpId)
        {
            lessons = lessons.Where(l => l.GroupId == grpId);
        }

        if (query.TeacherContractId is { } teacherContractId)
        {
            lessons = lessons.Where(l =>
                l.TeacherContractId == teacherContractId || l.SubstituteTeacherContractId == teacherContractId);
        }

        if (query.DateFrom is { } dateFrom)
        {
            lessons = lessons.Where(l => l.LessonDt >= dateFrom);
        }

        //DateTo ჩათვლით: მისი ბოლომდე
        if (query.DateTo is { } dateTo)
        {
            DateTime dayAfter = dateTo.AddDays(1);
            lessons = lessons.Where(l => l.LessonDt < dayAfter);
        }

        if (query.LessonStatusId is { } lessonStatusId)
        {
            lessons = lessons.Where(l => l.LessonStatusId == lessonStatusId);
        }

        //შეუვსებელი: უკვე დაწყებული, ჩატარებული (სტატუსი 1) გაკვეთილი, რომელსაც მოსწავლეები ჰყავს, მაგრამ არცერთი
        //დამსწრედ არ არის მონიშნული
        if (query.Unfilled)
        {
            DateTime now = query.Now;
            lessons = lessons.Where(l =>
                l.LessonDt < now && l.LessonStatusId == HeldLessonStatusId && l.LessonsByStudents.Any() &&
                !l.LessonsByStudents.Any(s => s.Present));
        }

        return lessons;
    }

    //ბოლოს ID-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<LessonRowData> ApplySort(IQueryable<LessonRowData> query,
        IReadOnlyList<LessonSortField> sortFields)
    {
        IOrderedQueryable<LessonRowData>? ordered = null;
        foreach (LessonSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                ELessonSortField.LessonDt => Order(query, ordered, r => r.LessonDt, sortField.Ascending),
                ELessonSortField.GroupCode => Order(query, ordered, r => r.GroupCode, sortField.Ascending),
                ELessonSortField.CourseName => Order(query, ordered, r => r.CourseName, sortField.Ascending),
                ELessonSortField.TeacherName => Order(query, ordered, r => r.TeacherName, sortField.Ascending),
                ELessonSortField.SubstituteTeacherName => Order(query, ordered, r => r.SubstituteTeacherName,
                    sortField.Ascending),
                //სტატუსის ID-ით, როგორც Access-ის ჩამოსაშლელ სიაში
                ELessonSortField.LessonStatusName => Order(query, ordered, r => r.LessonStatusId, sortField.Ascending),
                ELessonSortField.StudentsCount => Order(query, ordered, r => r.StudentsCount, sortField.Ascending),
                ELessonSortField.PresentCount => Order(query, ordered, r => r.PresentCount, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.LessonId, true);
    }

    private static IOrderedQueryable<LessonRowData> Order<TKey>(IQueryable<LessonRowData> query,
        IOrderedQueryable<LessonRowData>? ordered, Expression<Func<LessonRowData, TKey>> keySelector, bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
