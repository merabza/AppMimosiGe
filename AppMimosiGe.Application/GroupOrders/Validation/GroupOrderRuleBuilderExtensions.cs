using FluentValidation;
using SystemTools.SharedKernel;

namespace AppMimosiGe.Application.GroupOrders.Validation;

internal static class GroupOrderRuleBuilderExtensions
{
    //წესის შეცდომა Error-იდან: კოდი და ტექსტი
    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> rule, Error error)
    {
        return rule.WithErrorCode(error.Code).WithMessage(error.Description);
    }
}
