using BackendCarcass.Api.Filters;
using BackendCarcass.Application.Identity;
using BackendCarcass.Application.Rights;
using Microsoft.Extensions.Logging;
using SystemTools.SystemToolsShared;

namespace AppMimosiGe.WebApi.Filters;

//წვდომა მენიუს პუნქტის უფლებით: ვისაც მენიუში "ახალი სასწავლო წელი" უჩანს, მას წლის დამატება და წლის ჯგუფების დახურვა
//შეუძლია. პუნქტი მხოლოდ Admin-ს აქვს, როგორც ხელფასები (D108)
public sealed class UserMustHaveNewAcademicYearRightsFilter : UserMenuRightsFilter
{
    public const string MenuKey = "newAcademicYear";

    // ReSharper disable once ConvertToPrimaryConstructor
    public UserMustHaveNewAcademicYearRightsFilter(IUserRightsRepository repo, IDatabaseAbstraction databaseAbstraction,
        ILogger<UserMustHaveNewAcademicYearRightsFilter> logger, ICurrentUser currentUser) : base([MenuKey], repo,
        logger, currentUser, databaseAbstraction)
    {
    }
}
