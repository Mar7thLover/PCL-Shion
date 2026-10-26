using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.IO;

namespace PCL.Shion.Core.Test.IO;

[TestClass]
public sealed class JsonFileStoreTest
{
    private string _directory = null!;
    private string FilePath => Path.Combine(_directory, "settings.json");

    [TestInitialize]
    public void Setup() => _directory = Path.Combine(Path.GetTempPath(), "shion-storage-" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [TestMethod]
    public void MissingAndCorruptFilesHaveDifferentStates()
    {
        Assert.AreEqual(JsonFileStatus.Missing, JsonFileStore.Read<Sample>(FilePath).Status);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{truncated");
        Assert.AreEqual(JsonFileStatus.Unreadable, JsonFileStore.Read<Sample>(FilePath).Status);
    }

    [TestMethod]
    public void CorruptPrimaryRecoversLastValidSaveAndDoesNotPoisonBackup()
    {
        JsonFileStore.Save(FilePath, new Sample(1, "first"));
        JsonFileStore.Save(FilePath, new Sample(2, "second"));
        File.WriteAllText(FilePath, "{truncated");
        var result = JsonFileStore.Read<Sample>(FilePath);
        Assert.AreEqual(JsonFileStatus.Recovered, result.Status);
        Assert.AreEqual(1, result.Value!.Number);
        JsonFileStore.Save(FilePath, result.Value);
        Assert.AreEqual(1, JsonFileStore.Read<Sample>(FilePath).Value!.Number);
        Assert.AreEqual(1, JsonSerializer.Deserialize<Sample>(File.ReadAllText(FilePath + ".bak"))!.Number);
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    public void MissingPrimaryCanRecoverBackup()
    {
        JsonFileStore.Save(FilePath, new Sample(1, "first"));
        JsonFileStore.Save(FilePath, new Sample(2, "second"));
        File.Delete(FilePath);
        Assert.AreEqual(JsonFileStatus.Recovered, JsonFileStore.Read<Sample>(FilePath).Status);
    }

    [TestMethod]
    public void LockedPrimaryMayReadBackupButMustNotOverwriteNewerData()
    {
        JsonFileStore.Save(FilePath, new Sample(1, "older"));
        JsonFileStore.Save(FilePath, new Sample(2, "newer"));
        using (var locked = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var result = JsonFileStore.Read<Sample>(FilePath);
            Assert.AreEqual(JsonFileStatus.Recovered, result.Status);
            Assert.IsFalse(result.CanPersist);
            Assert.Throws<IOException>(() => JsonFileStore.Save(FilePath, new Sample(3, "replacement")));
        }
        Assert.AreEqual(2, JsonFileStore.Read<Sample>(FilePath).Value!.Number);
    }

    [TestMethod]
    public void MissingPrimaryReportsCorruptBackupError()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath + ".bak", "{broken");
        var result = JsonFileStore.Read<Sample>(FilePath);
        Assert.AreEqual(JsonFileStatus.Unreadable, result.Status);
        Assert.IsInstanceOfType<JsonException>(result.Error);
        Assert.IsFalse(result.CanPersist);
    }

    [TestMethod]
    public async Task ConcurrentReadersNeverObservePartialJson()
    {
        JsonFileStore.Save(FilePath, new Sample(0, new string('x', 8192)));
        var writers = Enumerable.Range(1, 40).Select(i => Task.Run(() =>
            JsonFileStore.Save(FilePath, new Sample(i, new string('x', 8192))))).ToArray();
        var reader = Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                var result = JsonFileStore.Read<Sample>(FilePath);
                Assert.AreEqual(JsonFileStatus.Loaded, result.Status);
                Assert.AreEqual(8192, result.Value!.Text.Length);
            }
        });
        await Task.WhenAll(writers.Append(reader));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    public void SerializationFailurePreservesOriginal()
    {
        JsonFileStore.Save(FilePath, new Sample(7, "saved"));
        var before = File.ReadAllText(FilePath);
        Assert.Throws<InvalidOperationException>(() => JsonFileStore.Save(FilePath, new Broken()));
        Assert.AreEqual(before, File.ReadAllText(FilePath));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [TestMethod]
    public void PrivateJsonFilesAndBackupsAreOwnerOnlyOnUnix()
    {
        if (OperatingSystem.IsWindows()) return;
        JsonFileStore.Save(FilePath, new Sample(1, "token"));
        JsonFileStore.Save(FilePath, new Sample(2, "new-token"));
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath));
        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(FilePath + ".bak"));
    }

    [TestMethod]
    public void FailedReplacementLeavesExistingFileAndCleansTemporaryFile()
    {
        Directory.CreateDirectory(_directory);
        var destination = Path.Combine(_directory, "directory-not-file");
        Directory.CreateDirectory(destination);
        try
        {
            AtomicFile.WriteAllText(destination, "data");
            Assert.Fail("Replacing a directory with a file must fail.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Windows 对目录目标返回拒绝访问，Unix 返回 IO 错误；都必须保留目标。
        }
        Assert.IsTrue(Directory.Exists(destination));
        Assert.IsEmpty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public sealed record Sample(int Number, string Text);
    public sealed class Broken { public string Value => throw new InvalidOperationException("serialization failed"); }
}
