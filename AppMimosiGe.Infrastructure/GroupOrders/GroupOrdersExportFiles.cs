using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using Microsoft.Extensions.Logging;

namespace AppMimosiGe.Infrastructure.GroupOrders;

//ვიზარდი 2-ის ფაილები საქაღალდეში GroupOrders:ExportFolder (D195). საქაღალდე, თუ არ არის, იქმნება. არსებულ ფაილს
//ჩაწერა არ გადაწერს: იმავე წამში მეორე გაშვება ვერ ჩაწერს და არაფერს წაშლის
public sealed class GroupOrdersExportFiles(GroupOrdersSettings settings, ILogger<GroupOrdersExportFiles> logger)
    : IGroupOrdersExportFiles
{
    public async Task<bool> TryWrite(string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(Folder(), fileName);
        try
        {
            Directory.CreateDirectory(Folder());
            await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or SecurityException or NotSupportedException or ArgumentException)
        {
            logger.LogError(exception, "Group orders export file {Path} could not be written", path);
            return false;
        }
    }

    public async Task<byte[]?> TryRead(string fileName, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(Folder(), fileName);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    //საქაღალდის გარეშე handler-ი ფაილს არ ითხოვს (ExportFolderIsNotConfigured)
    private string Folder()
    {
        return settings.ExportFolder ??
               throw new InvalidOperationException("The group orders export folder is not configured");
    }
}
