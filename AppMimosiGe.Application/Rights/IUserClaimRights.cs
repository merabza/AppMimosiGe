using System.Threading;
using System.Threading.Tasks;

namespace AppMimosiGe.Application.Rights;

/// <summary>
///     მიმდინარე მომხმარებლის სპეციალური უფლებები (carcass-ის AppClaim) იმ შემთხვევისთვის, როცა უფლება მთელ endpoint-ს
///     კი არ ხურავს (ამას UserClaimRightsFilter აკეთებს), არამედ handler-ის ქცევას ცვლის. რეალიზაცია
///     AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IUserClaimRights
{
    //false, თუ მომხმარებლის არცერთ როლს არ აქვს ეს უფლება ან უფლების დადგენა ვერ მოხერხდა
    Task<bool> HasClaim(string claimKey, CancellationToken cancellationToken = default);
}
