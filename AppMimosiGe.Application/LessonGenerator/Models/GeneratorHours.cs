using System;

namespace AppMimosiGe.Application.LessonGenerator.Models;

/// <summary>
///     საათების შედარება (D153): ორი მნიშვნელობა განსხვავებულად მხოლოდ მაშინ ითვლება, როცა სხვაობა დასაშვებ ზღვარს
///     (Tolerance) აღემატება. ზღვარი float-ის დამრგვალების ცდომილებაზე ბევრად დიდია და საათის რეალურ სხვაობაზე
///     (წუთი ≈ 0.017) ბევრად მცირე
/// </summary>
public static class GeneratorHours
{
    public const float Tolerance = 0.0001f;

    public static bool AreDifferent(float first, float second)
    {
        return Math.Abs(first - second) > Tolerance;
    }
}
