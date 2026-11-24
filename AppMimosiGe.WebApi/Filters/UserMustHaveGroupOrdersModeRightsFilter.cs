using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ბრძანებების რეჟიმი" უჩანს, მას ვიზარდები და ვიზარდი 2-ის ფაილის
//გადმოწერა შეუძლია (D184). პუნქტი მხოლოდ Admin-ს აქვს, როგორც "ახალი სასწავლო წელი"
public sealed class UserMustHaveGroupOrdersModeRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "groupOrdersMode";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveGroupOrdersModeRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveGroupOrdersModeRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo,
        logger, currentUser, databaseAbstraction)
    {
    }
}
