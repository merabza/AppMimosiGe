using System;

namespace AppMimosiGe.Application.BankTransactions;

/// <summary>
///     ბანკთან ინტეგრირებული ანგარიში (D167): BankAccounts-ის ჩანაწერი და დღე, საიდანაც მისი გადახდები მხოლოდ ბანკიდან
///     შემოდის (ჩარიცხვები ამ დღიდან მოდის, ხელით შეყვანა ამ დღიდან აიკრძალება)
/// </summary>
public sealed record IntegratedBankAccount(int BankAccountId, DateTime StartDate);
