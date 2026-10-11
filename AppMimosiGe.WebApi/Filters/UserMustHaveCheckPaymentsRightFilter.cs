using AppMimosiGe.Application.Payments;
using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//"შემოწმებულია" ალამს სიიდან მხოლოდ გადახდების შემოწმების სპეციალური უფლების (AppClaim) მქონე როლი ცვლის (D77, D152)
public sealed class UserMustHaveCheckPaymentsRightFilter : UserClaimRightsFilter
{
    public const string ClaimKey = PaymentClaims.CheckPayments;

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveCheckPaymentsRightFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveCheckPaymentsRightFilter> logger, ICurrentUser currentUser) : base(ClaimKey, repo, logger,
        currentUser, databaseAbstraction)
    {
    }
}
