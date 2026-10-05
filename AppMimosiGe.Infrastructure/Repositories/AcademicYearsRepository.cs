using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.AcademicYears;
using AppMimosiGe.Application.AcademicYears.Models;
using AppMimosiGeShared.Contracts.V1.Responses;
using Microsoft.EntityFrameworkCore;
using MimosiGeCore.Application.Abstractions;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Infrastructure.Repositories;

public sealed class AcademicYearsRepository(IMimosiGeDbContext context) : IAcademicYearsRepository
{
    public Task<List<AcademicYear>> GetAcademicYears(CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<AcademicYearInfoResponse>> GetAcademicYearsInfo(CancellationToken cancellationToken = default)
    {
        return context.AcademicYears.AsNoTracking().OrderBy(ay => ay.StartDate).Select(ay =>
            new AcademicYearInfoResponse(ay.AyId, ay.AcademicYearName, ay.StartDate, ay.FinishDate,
                ay.StudentContracts.Count, ay.Groups.Count,
                ay.Groups.Count(g => g.VoidDate == null || g.VoidDate > ay.FinishDate))).ToListAsync(cancellationToken);
    }

    public void Add(AcademicYear academicYear)
    {
        context.AcademicYears.Add(academicYear);
    }

    public Task<List<GroupToClose>> GetGroupsToClose(int ayId, DateTime closeDate,
        CancellationToken cancellationToken = default)
    {
        return context.Groups.AsNoTracking()
            .Where(g => g.AcademicYearId == ayId && (g.VoidDate == null || g.VoidDate > closeDate))
            .OrderBy(g => g.GrpId).Select(g => new GroupToClose(g.GrpId, g.GroupCode, g.Course.CourseName))
            .ToListAsync(cancellationToken);
    }
}
