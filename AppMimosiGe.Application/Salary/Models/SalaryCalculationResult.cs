using System.Collections.Generic;

namespace AppMimosiGe.Application.Salary.Models;

public sealed record SalaryCalculationResult(
    IReadOnlyList<CalculatedSalaryPart> LessonParts,
    IReadOnlyList<CalculatedSalaryLine> Lines);
