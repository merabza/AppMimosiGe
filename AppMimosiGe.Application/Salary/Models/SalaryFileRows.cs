using System;

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

/// <summary>
///     დეკლარაციის ფაილის სტრიქონის მონაცემები (უწყისის ერთი სტრიქონი). RsQuoteTypeId კონტრაქტის განაცემის სახეა
/// </summary>
public sealed record DeclarationFileRow(
    int SaId,
    string PersonalId,
    string? LegalName,
    string FirstName,
    string LastName,
    string? LegalAddress,
    string CountryCode,
    int? RsQuoteTypeId,
    decimal AmountGross,
    DateTime TransferDate);

/// <summary>
///     ჩამოსატვირთი ფაილი
/// </summary>
public sealed record SalaryFile(string FileName, byte[] Content);
