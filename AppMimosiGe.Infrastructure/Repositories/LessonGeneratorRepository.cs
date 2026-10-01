using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.LessonGenerator;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class LessonGeneratorRepository(IMimosiGeDbContext context) : ILessonGeneratorRepository
{
    public Task<DateTime?> GetLastOperationMonth(CancellationToken cancellationToken = default)
    {
        return context.OperationMonths.MaxAsync(x => (DateTime?)x.MonthDate, cancellationToken);
    }

    public void AddOperationMonths(IEnumerable<DateTime> months)
    {
        context.OperationMonths.AddRange(months.Select(month => new OperationMonth { MonthDate = month }));
    }

    public async Task MarkAllDirty(CancellationToken cancellationToken = default)
    {
        foreach (Group group in await context.Groups.ToListAsync(cancellationToken))
        {
            group.DirtyLessons = true;
        }

        foreach (StudentContract studentContract in await context.StudentContracts.ToListAsync(cancellationToken))
        {
            studentContract.DirtyNextPayDate = true;
        }
    }

    public Task<List<int>> GetGroupIds(bool onlyDirty, CancellationToken cancellationToken = default)
    {
        return context.Groups.Where(g => !onlyDirty || g.DirtyLessons).OrderBy(g => g.GrpId).Select(g => g.GrpId)
            .ToListAsync(cancellationToken);
    }

    //ცალ-ცალკე მოთხოვნებით (AsSplitQuery), რომ გაკვეთილები და მოსწავლეები ერთმანეთზე არ გამრავლდეს
    public Task<Group?> GetGroupForGeneration(int grpId, bool forChange, CancellationToken cancellationToken = default)
    {
        IQueryable<Group> groups = context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).ThenInclude(d => d.LessonStartTime).Include(g => g.Lessons)
            .ThenInclude(l => l.LessonsByStudents).Include(g => g.LessonsCheckCreateErrorLogs).AsSplitQuery();
        if (!forChange)
        {
            groups = groups.AsNoTracking();
        }

        return groups.SingleOrDefaultAsync(g => g.GrpId == grpId, cancellationToken);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x => scIds.Contains(x.ScId)).ToListAsync(cancellationToken);
    }

    public Task<Dictionary<int, string>> GetErrorLogTexts(CancellationToken cancellationToken = default)
    {
        return context.ErrorLogTexts.AsNoTracking().ToDictionaryAsync(x => x.EltId, x => x.Text, cancellationToken);
    }

    public Task<List<LessonGeneratorLogRowResponse>> GetLog(int? grpId, CancellationToken cancellationToken = default)
    {
        IQueryable<LessonCheckCreateErrorLog> logs = context.LessonsCheckCreateErrorLogs.AsNoTracking();
        if (grpId is not null)
        {
            logs = logs.Where(x => x.GroupId == grpId);
        }

        return logs.OrderBy(x => x.Group.GroupCode).ThenBy(x => x.GroupId).ThenBy(x => x.LessonDate).ThenBy(x => x.Id)
            .Select(x => new LessonGeneratorLogRowResponse(x.Id, x.CreatedDate, x.GroupId, x.Group.GroupCode,
                x.ErrorLogTextId, x.ErrorLogText.Text, x.LessonDate, x.LessonId)).ToListAsync(cancellationToken);
    }

    public void AddLesson(Lesson lesson)
    {
        context.Lessons.Add(lesson);
    }

    public void RemoveLesson(Lesson lesson)
    {
        context.LessonsByStudents.RemoveRange(lesson.LessonsByStudents);
        context.Lessons.Remove(lesson);
    }

    public void RemoveLessonStudent(LessonByStudent lessonStudent)
    {
        context.LessonsByStudents.Remove(lessonStudent);
    }

    public void ReplaceLogs(Group group, IEnumerable<LessonCheckCreateErrorLog> logs)
    {
        context.LessonsCheckCreateErrorLogs.RemoveRange(group.LessonsCheckCreateErrorLogs);
        context.LessonsCheckCreateErrorLogs.AddRange(logs);
    }
}
