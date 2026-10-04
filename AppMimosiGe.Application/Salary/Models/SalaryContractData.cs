namespace AppMimosiGe.Application.Salary.Models;

/// <summary>
///     თანამშრომლის კონტრაქტის ხელფასის პარამეტრები
/// </summary>
public sealed record SalaryContractData(int Id, bool PensionScheme, bool IndEnt, bool NextMonth);
