namespace AppMimosiGe.Application.Groups.Models;

/// <summary>
///     ჯგუფის მდგომარეობა სიის ფილტრში. გაუქმებულია ჯგუფი, რომლის გაუქმების თარიღი დღეს ან წარსულშია
/// </summary>
public enum EGroupState
{
    Active,
    Voided
}
