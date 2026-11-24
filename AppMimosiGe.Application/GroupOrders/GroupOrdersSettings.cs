namespace AppMimosiGe.Application.GroupOrders;

/// <summary>
///     ბრძანებების პარამეტრები (კონფიგურაციის სექცია "GroupOrders", D193). ორგანიზაცია და ხელმომწერი გამოცემისას
///     ბრძანებაში იწერება, რომ ბრძანება ყოველთვის ისე დაიბეჭდოს, როგორც გამოიცა. სექციის გარეშე ნაგულისხმევი
///     მნიშვნელობები მოქმედებს
/// </summary>
public sealed class GroupOrdersSettings
{
    public const string SectionName = "GroupOrders";

    public string OrganizationName { get; set; } = "ცოდნის სახლი მიმოსი";

    public string SignerTitle { get; set; } = "დირექტორი";

    public string SignerName { get; set; } = "ნატალია ჭყონია";
}
