namespace AppMimosiGe.Application.GroupOrders;

public static class GroupOrderClaims
{
    //წარსული თარიღის ბრძანებები და საწყისი ბრძანების შეცვლა (AppClaim, D184, D196, D210). გასაღები იგივეა, რაც
    //MimosiGeDbTools-ის MimNewAppClaimsRulesCreator-ში (E1); ადმინისტრატორი მას ავტომატურად იღებს
    public const string ChangePastGroupOrders = "ChangePastGroupOrders";
}
