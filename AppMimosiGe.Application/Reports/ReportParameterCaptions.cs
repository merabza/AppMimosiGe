namespace AppMimosiGe.Application.Reports;

/// <summary>
///     პარამეტრების წარწერები Access-ის FrmMain-იდან და რეპორტების სათაურებიდან
/// </summary>
public static class ReportParameterCaptions
{
    public const string StartDate = "თარიღიდან";
    public const string EndDate = "თარიღამდე";

    //რეპორტი, რომელსაც მხოლოდ ერთი თარიღი აქვს: მდგომარეობა ამ დღისთვის
    public const string ForDate = "თარიღისთვის";

    //თვის რეპორტი (r01): თარიღის თვე, Access-ის Year/Month(txtEndDate)
    public const string Month = "თვე";
    public const string Teacher = "მასწავლებელი";
    public const string Course = "საგანი";
    public const string Student = "მოსწავლე";
}
