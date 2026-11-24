namespace AppMimosiGe.Application.GroupOrders.Models;

/// <summary>
///     ბრძანებების რეჟიმის ფორმულები (D187), რომლებსაც GroupOrderFormulasRule ამოწმებს
/// </summary>
public enum EGroupOrderFormula
{
    /// <summary>
    ///     4 კვირის საათები = 4 × კოეფიციენტი × განრიგის კვირის საათები
    /// </summary>
    Hours,

    /// <summary>
    ///     4 კვირის გადასახადი = საათის ფასი × 4 კვირის საათები (სხვაობა 0.01-მდე)
    /// </summary>
    Fee
}
