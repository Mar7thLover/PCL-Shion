using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class SecretsTest
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("|")]
    public void ParseServerListShouldBeEmptyWhenNotConfigured(string? raw)
    {
        // 未配置时若返回 [""]，联机页会拿空地址去请求并弹出“发生意外错误”
        CollectionAssert.AreEqual(Array.Empty<string>(), Secrets.ParseServerList(raw));
    }

    [TestMethod]
    public void ParseServerListShouldSplitAndTrim()
    {
        CollectionAssert.AreEqual(
            new[] { "https://a.example", "https://b.example" },
            Secrets.ParseServerList(" https://a.example | |https://b.example "));
    }
}
