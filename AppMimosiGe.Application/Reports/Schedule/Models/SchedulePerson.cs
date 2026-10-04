namespace AppMimosiGe.Application.Reports.Schedule.Models;

/// <summary>
///     მასწავლებლის ან მოსწავლის კონტრაქტის ადამიანი და კონტრაქტის ნომერი
/// </summary>
public sealed record SchedulePerson(string LastName, string FirstName, string ContractNumber)
{
    //"გვარი სახელი" (Access-ის [LastName]+" "+[FirstName])
    public string FullName => $"{LastName} {FirstName}";

    //"გვარი სახელი / ნომერი" (Access-ის [LastName]+' '+[FirstName]+' / '+[ContractNumber])
    public string NameWithNumber => $"{LastName} {FirstName} / {ContractNumber}";
}
