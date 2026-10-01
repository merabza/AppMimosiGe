using System;

namespace AppMimosiGe.Infrastructure.Repositories;

//გადახდების სიის სტრიქონი SQL-ში, რომ დალაგება და გვერდებად დაყოფა სერვერზე მოხდეს (EF პროექციას member-init-ით
//თარგმნის)
internal sealed class PaymentRowData
{
    public int Id { get; init; }
    public int StudentContractId { get; init; }
    public required string StudentName { get; init; }
    public DateTime PayDate { get; init; }
    public decimal Amount { get; init; }
    public string? Document { get; init; }
    public int? BankAccountId { get; init; }
    public string? BankName { get; init; }
    public bool Checked { get; init; }
}
