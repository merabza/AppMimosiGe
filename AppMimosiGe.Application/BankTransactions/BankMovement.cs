using System;

namespace AppMimosiGe.Application.BankTransactions;

/// <summary>
///     ბანკის ანგარიშზე შემოსული ჩარიცხვა, ბანკისგან დამოუკიდებელი ფორმით (D166). MovementId ბანკის უნიკალური
///     იდენტიფიკატორია (თიბისის movementId), რომლითაც ჩარიცხვა ორჯერ არ გატარდება (D164). დანიშნულების სამი ველი
///     (Purpose, AdditionalInformation, AdditionalDescription) მოსწავლის საძებნი ტექსტია, Partner* კი გადამხდელისაა
/// </summary>
public sealed record BankMovement(
    string MovementId,
    DateTime Date,
    DateTime? DocumentDate,
    decimal Amount,
    string Currency,
    string? Purpose,
    string? AdditionalInformation,
    string? AdditionalDescription,
    string? DocumentNumber,
    string? PartnerName,
    string? PartnerIban,
    string? PartnerBankCode,
    string? PartnerTaxCode,
    string? PartnerPersonalNumber);
