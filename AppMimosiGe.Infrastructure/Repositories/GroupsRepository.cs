using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.Groups;
using AppMimosiGe.Application.Groups.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class GroupsRepository(IMimosiGeDbContext context) : IGroupsRepository
{
    //ჯერ ფილტრი, დათვლა და დალაგება ხდება SQL სერვერზე, შემდეგ იტვირთება მხოლოდ საჭირო გვერდი
    public async Task<GroupsRowsDataResponse> GetRowsData(GroupsListQuery query,
        CancellationToken cancellationToken = default)
    {
        IQueryable<GroupRowData> rows = CreateRows(query);

        int count = await rows.CountAsync(cancellationToken);

        //გვერდი ფილტრის შეცვლის შემდეგ შეიძლება აღარ არსებობდეს. მაშინ ბოლო გვერდი ჩანს
        int offset = query.Offset;
        if (offset >= count && count > 0)
        {
            offset = (count - 1) / query.RowsCount * query.RowsCount;
        }

        List<GroupRowData> page = await ApplySort(rows, query.SortFields).Skip(offset).Take(query.RowsCount)
            .ToListAsync(cancellationToken);

        //ზომა "ადგილები-დასახელება", როგორც Access-ის ჩამოსაშლელ სიაში
        return new GroupsRowsDataResponse(count, offset, [
            .. page.Select(r => new GroupRowResponse(r.RowId, r.GrpId, r.GroupCode, r.AcademicYearName, r.CourseName,
                $"{r.GroupSize}-{r.GroupSizeName}", r.StudentStatusName, r.VoidDate, r.DirtyLessons, r.TeacherName,
                r.ActiveStudentsCount, r.StudentName, r.StartDate, r.EndDate))
        ]);
    }

    public Task<GroupResponse?> GetOne(int grpId, CancellationToken cancellationToken = default)
    {
        return context.Groups.AsNoTracking().Where(g => g.GrpId == grpId).Select(g => new GroupResponse(g.GrpId,
            g.AcademicYearId, g.GroupCode, g.CourseId, g.GroupSizeId, g.StudentStatusId, g.VoidDate, g.DirtyLessons,
            g.GroupsByTeachers.OrderBy(t => t.StartDate).ThenBy(t => t.Id).Select(t =>
                new GroupTeacherResponse(t.Id, t.TeacherContractId, t.SalarySchemaId, t.StartDate, t.EndDate)).ToList(),
            g.GroupsByStudents
                .OrderBy(s => s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName +
                              " / " + s.StudentContract.ContractNumber).ThenBy(s => s.StartDate).ThenBy(s => s.EndDate)
                .ThenBy(s => s.GbsId)
                .Select(s => new GroupStudentResponse(s.GbsId, s.StudentContractId,
                    s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName + " / " +
                    s.StudentContract.ContractNumber, s.FourWeekHours, s.FourWeekFee, s.OneHourFee, s.HoursCoefficient,
                    s.StartDate, s.EndDate, s.Note)).ToList(),
            g.GroupDayTimePlaces.OrderBy(d => d.StartDate).ThenBy(d => d.GdtpId).Select(d =>
                new GroupDayTimePlaceResponse(d.GdtpId, d.WeekDayId, d.LessonStartTimeId, d.HoursCount, d.RoomId,
                    d.StartDate, d.EndDate)).ToList())).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Group?> GetForChange(int grpId, CancellationToken cancellationToken = default)
    {
        return context.Groups.Include(g => g.GroupsByTeachers).Include(g => g.GroupsByStudents)
            .Include(g => g.GroupDayTimePlaces).SingleOrDefaultAsync(g => g.GrpId == grpId, cancellationToken);
    }

    public Task<bool> GroupCodeExists(int academicYearId, string groupCode, int exceptGrpId,
        CancellationToken cancellationToken = default)
    {
        return context.Groups.AnyAsync(
            g => g.AcademicYearId == academicYearId && g.GroupCode == groupCode && g.GrpId != exceptGrpId,
            cancellationToken);
    }

    public async Task<bool> IsInUse(int grpId, CancellationToken cancellationToken = default)
    {
        return await context.Lessons.AnyAsync(x => x.GroupId == grpId, cancellationToken) ||
               await context.SalaryLinesDetails.AnyAsync(x => x.GroupId == grpId, cancellationToken) ||
               await context.LessonsByStudents.AnyAsync(
                   x => x.GroupByStudent != null && x.GroupByStudent.GroupId == grpId, cancellationToken);
    }

    public Task<bool> AnyStudentRowIsInUse(IReadOnlyCollection<int> gbsIds,
        CancellationToken cancellationToken = default)
    {
        return context.LessonsByStudents.AnyAsync(
            x => x.GroupByStudentId != null && gbsIds.Contains(x.GroupByStudentId.Value), cancellationToken);
    }

    public Task<bool> TeacherContractExists(int id, CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> StudentContractExists(int scId, CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AnyAsync(x => x.ScId == scId, cancellationToken);
    }

    public Task<bool> WeekDayExists(int id, CancellationToken cancellationToken = default)
    {
        return context.WeekDays.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<bool> LessonStartTimeExists(int lstId, CancellationToken cancellationToken = default)
    {
        return context.LessonStartTimes.AnyAsync(x => x.LstId == lstId, cancellationToken);
    }

    public Task<bool> RoomExists(int id, CancellationToken cancellationToken = default)
    {
        return context.Rooms.AnyAsync(x => x.Id == id, cancellationToken);
    }

    public Task<int?> GetDefaultSalarySchemeId(int teacherContractId, CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.Where(x => x.Id == teacherContractId).Select(x => x.SalarySchemaByHoursId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<List<GroupTeacherContractLookupResponse>> GetTeacherContracts(
        CancellationToken cancellationToken = default)
    {
        return context.TeacherContracts.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                Name = x.TeacherHuman.LastName + " " + x.TeacherHuman.FirstName + " / " + x.ContractNumber,
                x.SalarySchemaByHoursId
            }).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new GroupTeacherContractLookupResponse(x.Id, x.Name, x.SalarySchemaByHoursId))
            .ToListAsync(cancellationToken);
    }

    public Task<List<GroupStudentContractLookupResponse>> GetStudentContracts(int academicYearId,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.AsNoTracking().Where(x => x.AcademicYearId == academicYearId)
            .Select(x => new
            {
                x.ScId,
                Name = x.StudentHuman.LastName + " " + x.StudentHuman.FirstName + " / " + x.ContractNumber,
                x.StudentContractDetails
            }).OrderBy(x => x.Name).ThenBy(x => x.ScId).Select(x => new GroupStudentContractLookupResponse(x.ScId,
                x.Name,
                x.StudentContractDetails.OrderBy(d => d.Id).Select(d => new StudentContractDetailResponse(d.Id,
                    d.CourseId, d.GroupSizeId, d.FourWeekHours, d.FourWeekFee, d.OneHourFee)).ToList()))
            .ToListAsync(cancellationToken);
    }

    public Task<List<LookupItemResponse>> GetWeekDays(CancellationToken cancellationToken = default)
    {
        return context.WeekDays.AsNoTracking().OrderBy(x => x.Id).Select(x => new LookupItemResponse(x.Id, x.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<LookupItemResponse>> GetLessonStartTimes(CancellationToken cancellationToken = default)
    {
        //სახელი "HH:mm" (LessonStartTime.Name) მხოლოდ ჩატვირთვის შემდეგ იქმნება
        return
        [
            .. (await context.LessonStartTimes.AsNoTracking().OrderBy(x => x.LstTime).ToListAsync(cancellationToken))
            .Select(x => new LookupItemResponse(x.LstId, x.Name))
        ];
    }

    public Task<List<LookupItemResponse>> GetRooms(CancellationToken cancellationToken = default)
    {
        return context.Rooms.AsNoTracking().OrderBy(x => x.RoomName).ThenBy(x => x.Id)
            .Select(x => new LookupItemResponse(x.Id, x.RoomName)).ToListAsync(cancellationToken);
    }

    public Task<List<StudentContract>> GetStudentContractsForChange(IReadOnlyCollection<int> scIds,
        CancellationToken cancellationToken = default)
    {
        return context.StudentContracts.Where(x => scIds.Contains(x.ScId)).ToListAsync(cancellationToken);
    }

    public void Add(Group group)
    {
        context.Groups.Add(group);
    }

    public void Remove(Group group)
    {
        RemoveRows(new GroupRemovedRows([.. group.GroupsByTeachers], [.. group.GroupsByStudents],
            [.. group.GroupDayTimePlaces]));
        context.Groups.Remove(group);
    }

    public void RemoveRows(GroupRemovedRows rows)
    {
        context.GroupsByTeachers.RemoveRange(rows.Teachers);
        context.GroupsByStudents.RemoveRange(rows.Students);
        context.GroupDayTimePlaces.RemoveRange(rows.DayTimePlaces);
    }

    private IQueryable<GroupRowData> CreateRows(GroupsListQuery query)
    {
        IQueryable<Group> groups = ApplyGroupFilter(context.Groups.AsNoTracking(), query);
        string? search = query.Search;
        DateTime today = query.Today;

        switch (query.FindMethod)
        {
            case EGroupFindMethod.Teacher:
                IQueryable<GroupByTeacher> teachers = groups.SelectMany(g => g.GroupsByTeachers);
                if (!string.IsNullOrEmpty(search))
                {
                    teachers = teachers.Where(t =>
                        (t.TeacherContract.TeacherHuman.LastName + " " + t.TeacherContract.TeacherHuman.FirstName)
                        .Contains(search) ||
                        (t.TeacherContract.TeacherHuman.FirstName + " " + t.TeacherContract.TeacherHuman.LastName)
                        .Contains(search));
                }

                return teachers.Select(t => new GroupRowData
                {
                    RowId = t.Id,
                    GrpId = t.GroupId,
                    GroupCode = t.Group.GroupCode,
                    AcademicYearName = t.Group.AcademicYear.AcademicYearName,
                    CourseName = t.Group.Course.CourseName,
                    GroupSize = t.Group.GroupSize.GrsSize,
                    GroupSizeName = t.Group.GroupSize.GrsName,
                    StudentStatusRate = t.Group.StudentStatus.Rate,
                    StudentStatusName = t.Group.StudentStatus.StudentStatusName,
                    VoidDate = t.Group.VoidDate,
                    DirtyLessons = t.Group.DirtyLessons,
                    TeacherName =
                        t.TeacherContract.TeacherHuman.LastName + " " + t.TeacherContract.TeacherHuman.FirstName,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate
                });
            case EGroupFindMethod.Student:
                IQueryable<GroupByStudent> students = groups.SelectMany(g => g.GroupsByStudents);
                if (!string.IsNullOrEmpty(search))
                {
                    students = students.Where(s =>
                        (s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName)
                        .Contains(search) ||
                        (s.StudentContract.StudentHuman.FirstName + " " + s.StudentContract.StudentHuman.LastName)
                        .Contains(search));
                }

                return students.Select(s => new GroupRowData
                {
                    RowId = s.GbsId,
                    GrpId = s.GroupId,
                    GroupCode = s.Group.GroupCode,
                    AcademicYearName = s.Group.AcademicYear.AcademicYearName,
                    CourseName = s.Group.Course.CourseName,
                    GroupSize = s.Group.GroupSize.GrsSize,
                    GroupSizeName = s.Group.GroupSize.GrsName,
                    StudentStatusRate = s.Group.StudentStatus.Rate,
                    StudentStatusName = s.Group.StudentStatus.StudentStatusName,
                    VoidDate = s.Group.VoidDate,
                    DirtyLessons = s.Group.DirtyLessons,
                    StudentName =
                        s.StudentContract.StudentHuman.LastName + " " + s.StudentContract.StudentHuman.FirstName,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate
                });
            default:
                if (!string.IsNullOrEmpty(search))
                {
                    groups = groups.Where(g => g.GroupCode.Contains(search));
                }

                //მიმდინარე მასწავლებელი და მოსწავლეები: ვისი [StartDate, EndDate) პერიოდიც დღეს მოიცავს
                return groups.Select(g => new GroupRowData
                {
                    RowId = g.GrpId,
                    GrpId = g.GrpId,
                    GroupCode = g.GroupCode,
                    AcademicYearName = g.AcademicYear.AcademicYearName,
                    CourseName = g.Course.CourseName,
                    GroupSize = g.GroupSize.GrsSize,
                    GroupSizeName = g.GroupSize.GrsName,
                    StudentStatusRate = g.StudentStatus.Rate,
                    StudentStatusName = g.StudentStatus.StudentStatusName,
                    VoidDate = g.VoidDate,
                    DirtyLessons = g.DirtyLessons,
                    TeacherName = g.GroupsByTeachers
                        .Where(t => t.StartDate <= today && (t.EndDate == null || t.EndDate > today))
                        .OrderByDescending(t => t.StartDate)
                        .Select(t => t.TeacherContract.TeacherHuman.LastName + " " +
                                     t.TeacherContract.TeacherHuman.FirstName).FirstOrDefault(),
                    ActiveStudentsCount = g.GroupsByStudents.Count(s =>
                        s.StartDate <= today && (s.EndDate == null || s.EndDate > today))
                });
        }
    }

    //ჯგუფის გაუქმების თარიღი [StartDate, VoidDate) პერიოდის ბოლოა: ამ დღიდან ჯგუფი გაუქმებულია
    private static IQueryable<Group> ApplyGroupFilter(IQueryable<Group> groups, GroupsListQuery query)
    {
        if (query.AcademicYearId is { } academicYearId)
        {
            groups = groups.Where(g => g.AcademicYearId == academicYearId);
        }

        if (query.CourseId is { } courseId)
        {
            groups = groups.Where(g => g.CourseId == courseId);
        }

        if (query.GroupSizeId is { } groupSizeId)
        {
            groups = groups.Where(g => g.GroupSizeId == groupSizeId);
        }

        if (query.StudentStatusId is { } studentStatusId)
        {
            groups = groups.Where(g => g.StudentStatusId == studentStatusId);
        }

        DateTime today = query.Today;
        return query.State switch
        {
            EGroupState.Active => groups.Where(g => g.VoidDate == null || g.VoidDate > today),
            EGroupState.Voided => groups.Where(g => g.VoidDate != null && g.VoidDate <= today),
            _ => groups
        };
    }

    //ბოლოს RowId-ით, რომ გვერდებად დაყოფა ერთნაირ მნიშვნელობებზეც მდგრადი იყოს
    private static IOrderedQueryable<GroupRowData> ApplySort(IQueryable<GroupRowData> query,
        IReadOnlyList<GroupSortField> sortFields)
    {
        IOrderedQueryable<GroupRowData>? ordered = null;
        foreach (GroupSortField sortField in sortFields)
        {
            ordered = sortField.Field switch
            {
                EGroupSortField.GroupCode => Order(query, ordered, r => r.GroupCode, sortField.Ascending),
                EGroupSortField.AcademicYearName => Order(query, ordered, r => r.AcademicYearName, sortField.Ascending),
                EGroupSortField.CourseName => Order(query, ordered, r => r.CourseName, sortField.Ascending),
                EGroupSortField.GroupSizeName => Order(query, ordered, r => r.GroupSize, sortField.Ascending),
                EGroupSortField.StudentStatusName => Order(query, ordered, r => r.StudentStatusRate,
                    sortField.Ascending),
                EGroupSortField.VoidDate => Order(query, ordered, r => r.VoidDate, sortField.Ascending),
                EGroupSortField.DirtyLessons => Order(query, ordered, r => r.DirtyLessons, sortField.Ascending),
                EGroupSortField.TeacherName => Order(query, ordered, r => r.TeacherName, sortField.Ascending),
                EGroupSortField.ActiveStudentsCount => Order(query, ordered, r => r.ActiveStudentsCount,
                    sortField.Ascending),
                EGroupSortField.StudentName => Order(query, ordered, r => r.StudentName, sortField.Ascending),
                EGroupSortField.StartDate => Order(query, ordered, r => r.StartDate, sortField.Ascending),
                EGroupSortField.EndDate => Order(query, ordered, r => r.EndDate, sortField.Ascending),
                _ => throw new ArgumentOutOfRangeException(nameof(sortFields), sortField.Field, "უცნობი დალაგების ველი")
            };
        }

        return Order(query, ordered, r => r.RowId, true);
    }

    private static IOrderedQueryable<GroupRowData> Order<TKey>(IQueryable<GroupRowData> query,
        IOrderedQueryable<GroupRowData>? ordered, Expression<Func<GroupRowData, TKey>> keySelector, bool ascending)
    {
        if (ordered is null)
        {
            return ascending ? query.OrderBy(keySelector) : query.OrderByDescending(keySelector);
        }

        return ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }
}
