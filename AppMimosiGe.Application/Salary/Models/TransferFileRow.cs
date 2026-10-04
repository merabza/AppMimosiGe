namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     გადარიცხვის ფაილის სტრიქონის მონაცემები (უწყისის ერთი სტრიქონი). MonthName სტრიქონის თვის ქართული სახელია
///     (GeoMonths), QuoteTypeName კონტრაქტის განაცემის სახე
/// </summary>
public sealed record TransferFileRow(
    string? BankAccount,
    string? LegalName,
    string FirstName,
    string LastName,
    string PersonalId,
    decimal AmountNet,
    string? Description,
    string? QuoteTypeName,
    string MonthName,
    int Year);
