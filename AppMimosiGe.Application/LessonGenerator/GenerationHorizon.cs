using System;

namespace AppMimosiGe.Application.LessonGenerator;

/// <summary>
///     გენერატორის ჰორიზონტი: ბოლო სამუშაო თვის ბოლო დღე და რამდენი თვე დაემატა (dry-run-ში დაემატებოდა)
/// </summary>
public sealed record GenerationHorizon(DateTime HorizonEnd, int AddedMonthsCount);
