namespace AppMimosiGe.Application.BankTransactions.Models;

/// <param name="StudentHumanId">მოსწავლე</param>
/// <param name="Balance">მოსწავლის ანგარიშის ბალანსი ჩარიცხვის თარიღისთვის (უარყოფითი = ვალი, დადებითი = დეპოზიტი)</param>
/// <param name="MustPayToEnd">წლის ბოლომდე გადასახდელი (დეპოზიტების სიის MustPayToEnd; ვალის ჩათვლით)</param>
public sealed record DistributionAccount(int StudentHumanId, decimal Balance, decimal MustPayToEnd);
