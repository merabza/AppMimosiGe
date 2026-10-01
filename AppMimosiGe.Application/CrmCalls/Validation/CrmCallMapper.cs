using AppMimosiGeShared.Contracts.V1.Requests;
using MimosiGeCore.Domain.Models;

namespace AppMimosiGe.Application.CrmCalls.Validation;

public static class CrmCallMapper
{
    //ვალიდატორი AnswerTypeId-ის შევსებას მანამდე ამოწმებს
    public static void ApplyFields(CrmCall crmCall, CrmCallRequest request)
    {
        crmCall.StudentContractId = request.StudentContractId;
        crmCall.CallTypeId = request.CallTypeId;
        //ზარის თარიღი დროით (Access-ის ნაგულისხმევი Now())
        crmCall.CallDate = request.CallDate;
        crmCall.AnswerTypeId = request.AnswerTypeId!.Value;
        crmCall.CallConversation = NormalizeText(request.CallConversation);
        //"უნდა გადაიხადოს თარიღამდე" დღეა: ბალანსების "დარეკვის ფილტრი" მას დღეს ადარებს
        crmCall.MustPayDate = request.MustPayDate?.Date;
    }

    //ცარიელი ან მხოლოდ ჰარებიანი ტექსტი NULL-ად ინახება
    public static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
