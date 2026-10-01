using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.Payments.Validation;

public static class PaymentMapper
{
    //Checked-ის შეცვლის უფლებას handler-ი ამოწმებს მანამდე, სანამ ველებს entity-ზე გადაიტანს
    public static void ApplyFields(Payment payment, PaymentRequest request)
    {
        payment.StudentContractId = request.StudentContractId;
        //Access-ში გადახდის თარიღს დრო არ ჰქონდა (ნაგულისხმევი Date())
        payment.PayDate = request.PayDate.Date;
        payment.Amount = request.Amount;
        payment.Document = NormalizeText(request.Document);
        payment.BankAccountId = request.BankAccountId;
        payment.Checked = request.Checked;
    }

    //ცარიელი ან მხოლოდ ჰარებიანი ტექსტი NULL-ად ინახება
    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
