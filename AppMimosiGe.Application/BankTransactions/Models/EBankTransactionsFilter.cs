namespace AppMimosiGe.Application.BankTransactions.Models;

/// <summary>
///     ჩარიცხვების სიის ფილტრი: None = ყველა; Unallocated = ბოლომდე გაუნაწილებელი (არ არის მონიშნული „არ არის მოსწავლის
///     გადახდა“ და განაწილებული ჯამი ჩარიცხულზე ნაკლებია); Ignored = „არ არის მოსწავლის გადახდა“
/// </summary>
public enum EBankTransactionsFilter
{
    None,
    Unallocated,
    Ignored
}
