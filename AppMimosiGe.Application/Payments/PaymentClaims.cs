namespace AppMimosiGe.Application.Payments;

public static class PaymentClaims
{
    //გადახდების შემოწმება (AppClaim): "შემოწმებულია" ალამის ცვლა და შემოწმებული გადახდის შეცვლა ან წაშლა.
    //გასაღები იგივეა, რაც MimosiGeDbTools-ის MimNewAppClaimsRulesCreator.CheckPaymentsKey (ადმინისტრატორი მას
    //ავტომატურად იღებს) და ფრონტის checkPaymentsClaim
    public const string CheckPayments = "CheckPayments";
}
