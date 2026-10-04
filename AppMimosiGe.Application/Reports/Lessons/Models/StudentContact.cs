using AppMimosiGe.Application.Reports.Schedule.Models;

namespace AppMimosiGe.Application.Reports.Lessons.Models;

/// <summary>
///     მოსწავლე კონტრაქტის ნომრით, მისი და გადამხდელის ("გვარი სახელი") ტელეფონები
/// </summary>
public sealed record StudentContact(SchedulePerson Student, string? StudentPhone, string PayerName, string? PayerPhone);
