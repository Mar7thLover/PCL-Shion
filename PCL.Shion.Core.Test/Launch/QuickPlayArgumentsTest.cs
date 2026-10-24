using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class QuickPlayArgumentsTest
{
    [TestMethod]
    [DataRow("mc.example.com", "mc.example.com", 25565)]
    [DataRow("mc.example.com:25570", "mc.example.com", 25570)]
    [DataRow("127.0.0.1:1", "127.0.0.1", 1)]
    [DataRow("[::1]:25570", "::1", 25570)]
    [DataRow("[::1]", "::1", 25565)]
    [DataRow("::1", "::1", 25565)]
    [DataRow("mc.example.com:", "mc.example.com", 25565)]
    [DataRow("mc.example.com:port", "mc.example.com", 25565)]
    [DataRow("mc.example.com:0", "mc.example.com", 25565)]
    [DataRow("mc.example.com:70000", "mc.example.com", 25565)]
    public void ParsesServerAddresses(string address, string host, int port)
    {
        Assert.AreEqual((host, port), QuickPlayArguments.ParseAddress(address));
    }
}
