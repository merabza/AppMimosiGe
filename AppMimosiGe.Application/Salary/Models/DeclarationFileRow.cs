using System;

namespace AppMimosiGe.Application.Salary.Models;

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
