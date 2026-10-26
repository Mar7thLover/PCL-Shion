using Avalonia.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Services;

namespace PCL.Shion.Test;

[TestClass]
public sealed class DiagnosticLogTest
{
    [TestMethod]
    [DataRow("Authorization: Bearer private-value")]
    [DataRow("--accessToken private-value --username demo")]
    [DataRow("{\"refreshToken\":\"private-value\"}")]
    [DataRow("client_secret=private-value&scope=demo")]
    [DataRow("Authorization: Basic private-value")]
    [DataRow("{\"password\":\"first second private-value\"}")]
    [DataRow("--password 'first;second&private-value'")]
    [DataRow("{\"password\":\"first \\\"second private-value\\\"\"}")]
    [DataRow("--network-secret private-value")]
    public void AuthenticationSecretsAreRedacted(string input)
    {
        var result = DiagnosticLog.Redact(input);
        Assert.DoesNotContain("private-value", result);
        Assert.Contains("[redacted]", result);
    }

    [TestMethod]
    public void LogsRotateAndDisposedLoggerNeverThrows()
    {
        var directory = Path.Combine(Path.GetTempPath(), "shion-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var log = new DiagnosticLog(directory);
            for (var i = 0; i < 90; i++) log.Log(LogEventLevel.Error, "Test", null, new string('x', 32768));
            log.Dispose();
            log.Log(LogEventLevel.Error, "Test", null, "after-dispose");
            var files = Directory.GetFiles(directory);
            Assert.HasCount(2, files);
            Assert.IsTrue(files.All(path => new FileInfo(path).Length < 2 * 1024 * 1024 + 65536));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
