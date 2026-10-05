using System;

namespace AppMimosiGe.Infrastructure.Time;

//„დღეს" და „ახლა" საქართველოს დროით, სერვერის დროის სარტყლისგან დამოუკიდებლად (D140):
//production Linux სერვერზე მუშაობს, რომლის სარტყელი შეიძლება UTC იყოს
public sealed class GeorgiaTimeProvider : TimeProvider
{
    public const string TimeZoneId = "Asia/Tbilisi";

    public override TimeZoneInfo LocalTimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
}
