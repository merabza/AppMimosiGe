using System.Threading;
using System.Threading.Tasks;

namespace AppMimosiGe.Application.GroupOrders;

/// <summary>
///     ვიზარდი 2-ის ფაილები საქაღალდეში GroupOrders:ExportFolder (D195). რეალიზაცია AppMimosiGe.Infrastructure-შია
/// </summary>
public interface IGroupOrdersExportFiles
{
    //ფაილი იწერება მთლიანად, ან საერთოდ არ იწერება; false: ვერ ჩაიწერა (საქაღალდე, უფლება, უკვე არსებული სახელი)
    Task<bool> TryWrite(string fileName, byte[] content, CancellationToken cancellationToken = default);

    //null: ფაილი არ არის
    Task<byte[]?> TryRead(string fileName, CancellationToken cancellationToken = default);
}
