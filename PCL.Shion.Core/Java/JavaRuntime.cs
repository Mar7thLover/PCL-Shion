namespace PCL.Shion.Core.Java;

/// <summary>
/// 一个可用的 Java 运行时。
/// </summary>
/// <param name="Executable">启动游戏使用的可执行文件，Windows 上存在 javaw.exe 时为 javaw.exe。</param>
/// <param name="MajorVersion">主版本号，1.8.0_x 为 8，21.0.10 为 21。</param>
/// <param name="Version">完整版本号，取自 JAVA_VERSION 或 java -version 输出。</param>
/// <param name="Is64Bit">是否为 64 位运行时。</param>
/// <param name="Source">发现来源：JAVA_HOME、PATH、Launcher、StoreLauncher、Installed、Registry。</param>
public sealed record JavaRuntime(string Executable, int MajorVersion, string Version, bool Is64Bit, string Source);
