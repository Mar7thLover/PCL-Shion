using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Minecraft;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class MavenNameTest
{
    [TestMethod]
    [DataRow("com.mojang:authlib:10.0.77", "com/mojang/authlib/10.0.77/authlib-10.0.77.jar")]
    [DataRow("org.lwjgl:lwjgl:3.3.3:natives-windows", "org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar")]
    [DataRow("de.oceanlabs.mcp:mcp_config:1.21.1-20240808.132146@zip", "de/oceanlabs/mcp/mcp_config/1.21.1-20240808.132146/mcp_config-1.21.1-20240808.132146.zip")]
    [DataRow("net.minecraftforge:forge:1.21.1-52.1.20:universal@jar", "net/minecraftforge/forge/1.21.1-52.1.20/forge-1.21.1-52.1.20-universal.jar")]
    [DataRow("net.fabricmc:fabric-loader:0.16.5", "net/fabricmc/fabric-loader/0.16.5/fabric-loader-0.16.5.jar")]
    public void GetPathFollowsMavenLayout(string name, string expected)
    {
        Assert.AreEqual(expected, MavenName.GetPath(name));
    }

    [TestMethod]
    public void ExplicitClassifierOverridesNameClassifier()
    {
        Assert.AreEqual(
            "org/lwjgl/lwjgl-platform/2.9.4/lwjgl-platform-2.9.4-natives-windows.jar",
            MavenName.GetPath("org.lwjgl:lwjgl-platform:2.9.4", "natives-windows"));
        Assert.AreEqual(
            "a/b/c/1.0/c-1.0-natives-linux.jar",
            MavenName.GetPath("a.b:c:1.0:other", "natives-linux"));
    }

    [TestMethod]
    [DataRow("org.lwjgl:lwjgl:3.3.3", "org.lwjgl:lwjgl")]
    [DataRow("org.lwjgl:lwjgl:3.3.3:natives-windows", "org.lwjgl:lwjgl:natives-windows")]
    [DataRow("org.lwjgl:lwjgl:3.4.0:natives-windows", "org.lwjgl:lwjgl:natives-windows")]
    [DataRow("de.oceanlabs.mcp:mcp_config:1.21.1@zip", "de.oceanlabs.mcp:mcp_config")]
    public void GetKeyIgnoresVersionAndExtension(string name, string expected)
    {
        Assert.AreEqual(expected, MavenName.GetKey(name));
    }

    [TestMethod]
    [DataRow("com.mojang")]
    [DataRow("com.mojang:authlib")]
    public void NamesWithoutVersionAreRejected(string name)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => MavenName.GetPath(name));
        Assert.ThrowsExactly<InvalidDataException>(() => MavenName.GetKey(name));
    }
}
