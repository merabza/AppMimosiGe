using System;

namespace AppMimosiGe.Application.Reports.Checks.Models;

public sealed record CheckGroup(int GroupId, string GroupCode, int CourseId, string CourseName, DateTime? VoidDate);
