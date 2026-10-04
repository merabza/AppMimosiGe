using System.Collections.Generic;

namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     crosstab-ის სტრიქონი: გასაღები და კვირის დღეების უჯრები WeekDays-ის რიგით
/// </summary>
public sealed record PivotRow<TKey>(TKey Key, IReadOnlyList<object?> Days);
