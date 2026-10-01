using System;

namespace AppMimosiGe.Infrastructure.Repositories;

//CRM ზარების სიის სტრიქონი SQL-ში, რომ დალაგება და გვერდებად დაყოფა სერვერზე მოხდეს (EF პროექციას member-init-ით
//თარგმნის)
internal sealed class CrmCallRowData
{
    public int Id { get; init; }
    public int StudentContractId { get; init; }
    public required string StudentName { get; init; }
    public DateTime CallDate { get; init; }
    public int CallTypeId { get; init; }
    public required string CallTypeName { get; init; }
    public int AnswerTypeId { get; init; }
    public required string AnswerTypeName { get; init; }
    public string? CallConversation { get; init; }
    public DateTime? MustPayDate { get; init; }
}
