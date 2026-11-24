using System;
using System.IO;
using System.Threading.Tasks;
using AppMimosiGe.Application.GroupOrders;
using AppMimosiGe.Infrastructure.GroupOrders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AppMimosiGe.Application.Tests.Infrastructure;

//ვიზარდი 2-ის ფაილები დროებით საქაღალდეში (D195)
public sealed class GroupOrdersExportFilesTests : IDisposable
{
    private const string FileName = "GroupOrders_20261124163000.json";

    private readonly string _folder =
        Path.Combine(Path.GetTempPath(), $"GroupOrdersExportFilesTests_{Guid.NewGuid():N}", "export");

    public void Dispose()
    {
        string root = Path.GetDirectoryName(_folder)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private GroupOrdersExportFiles Files()
    {
        return new GroupOrdersExportFiles(new GroupOrdersSettings { ExportFolder = _folder },
            NullLogger<GroupOrdersExportFiles>.Instance);
    }

    //საქაღალდე, თუ არ არის, იქმნება
    [Fact]
    public async Task TryWrite_WritesTheFileIntoANewFolder()
    {
        bool written = await Files().TryWrite(FileName, [1, 2, 3]);

        Assert.True(written);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(Path.Combine(_folder, FileName)));
    }

    //არსებულ ფაილს ჩაწერა არ გადაწერს
    [Fact]
    public async Task TryWrite_ExistingFile_IsNotOverwritten()
    {
        await Files().TryWrite(FileName, [1]);

        bool written = await Files().TryWrite(FileName, [2, 3]);

        Assert.False(written);
        Assert.Equal([1], await File.ReadAllBytesAsync(Path.Combine(_folder, FileName)));
    }

    //საქაღალდის ადგილას ფაილია: ჩაწერა ვერ ხერხდება
    [Fact]
    public async Task TryWrite_FolderCannotBeCreated_ReturnsFalse()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_folder)!);
        await File.WriteAllBytesAsync(_folder, [0]);

        Assert.False(await Files().TryWrite(FileName, [1]));
    }

    [Fact]
    public async Task TryRead_ReturnsTheWrittenFile()
    {
        await Files().TryWrite(FileName, [4, 5]);

        Assert.Equal([4, 5], await Files().TryRead(FileName));
    }

    [Fact]
    public async Task TryRead_MissingFile_IsNull()
    {
        Assert.Null(await Files().TryRead(FileName));
    }
}
