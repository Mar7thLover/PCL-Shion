using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class OfflineAccountTest
{
    [TestMethod]
    [DataRow("Notch", "b50ad385829d3141a2167e7d7539ba7f")]
    [DataRow("Steve", "5627dd98e6be3c21b8a8e92344183641")]
    [DataRow("Player_01", "f2d90b7a21fe3bcb8d1b250b3b440256")]
    public void UuidMatchesJavaNameUuidFromBytes(string name, string expected)
    {
        var account = OfflineAccount.Create(name);

        Assert.AreEqual(name, account.Name);
        Assert.AreEqual(expected, account.Uuid);
    }

    [TestMethod]
    [DataRow("Notch")]
    [DataRow("PCL_Shion")]
    [DataRow("a_B_9")]
    public void UuidIsNameBasedMd5WithVersionAndVariantBits(string name)
    {
        // 直接在十六进制文本上置位，与实现中的字节运算相互独立
        var digits = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{name}"))).ToLowerInvariant();
        var variant = (Convert.ToInt32(digits[16].ToString(), 16) & 0x3) | 0x8;
        var expected = digits[..12] + "3" + digits[13..16] + variant.ToString("x") + digits[17..];

        Assert.AreEqual(expected, OfflineAccount.Create(name).Uuid);
    }

    [TestMethod]
    public void UuidIsLowercaseWithoutDashes()
    {
        var uuid = OfflineAccount.Create("Steve").Uuid;

        Assert.AreEqual(32, uuid.Length);
        Assert.IsTrue(uuid.All(c => char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c)));
        Assert.AreEqual('3', uuid[12]);
        Assert.IsTrue("89ab".Contains(uuid[16]));
    }

    [TestMethod]
    public void AccessTokenIsTheUuid()
    {
        var account = OfflineAccount.Create("Steve");

        Assert.AreEqual(account.Uuid, account.AccessToken);
    }

    [TestMethod]
    public void SameNameYieldsSameAccountAndNamesAreCaseSensitive()
    {
        Assert.AreEqual(OfflineAccount.Create("Steve"), OfflineAccount.Create("Steve"));
        Assert.AreNotEqual(OfflineAccount.Create("Steve").Uuid, OfflineAccount.Create("steve").Uuid);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("Steve")]
    [DataRow("Player_01")]
    [DataRow("___")]
    [DataRow("0123456789abcdef")]
    public void AcceptsValidNames(string name)
    {
        Assert.IsTrue(OfflineAccount.IsValidName(name));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ab")]
    [DataRow("0123456789abcdefg")]
    [DataRow("has space")]
    [DataRow("dash-ed")]
    [DataRow("dot.ted")]
    [DataRow("名字名字")]
    [DataRow("Steve\n")]
    [DataRow("abc!")]
    public void RejectsInvalidNames(string name)
    {
        Assert.IsFalse(OfflineAccount.IsValidName(name));
    }
}
