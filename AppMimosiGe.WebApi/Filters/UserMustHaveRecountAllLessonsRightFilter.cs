using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//ყველა ჯგუფის გაკვეთილების გადაანგარიშება (Access-ის cmdRecountLessonsAllGroups) მხოლოდ ამ სპეციალური უფლების
//(AppClaim) მქონე როლს შეუძლია; DataSeederRules მას ადმინისტრატორს აძლევს
public sealed class UserMustHaveRecountAllLessonsRightFilter : UserClaimRightsFilter
{
    public const string ClaimKey = "RecountAllGroupsLessons";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveRecountAllLessonsRightFilter(IUserRightsRepository repo,
        IDatabaseAbstraction databaseAbstraction, ILogger<UserMustHaveRecountAllLessonsRightFilter> logger,
        ICurrentUser currentUser) : base(ClaimKey, repo, logger, currentUser, databaseAbstraction)
    {
    }
}
