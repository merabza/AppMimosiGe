namespace AppMimosiGe.Application.Reports.Finance.Models;

/// <summary>
///     შავი სიის ადამიანი: გვარი, სახელი, პირადი ნომერი
/// </summary>
public sealed record Debtor(string LastName, string FirstName, string PersonalId);
