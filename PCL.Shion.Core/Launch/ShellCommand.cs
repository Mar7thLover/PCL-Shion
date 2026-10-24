using System.Diagnostics;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Launch;

/// <summary>
/// 用系统 shell 执行用户填写的命令：Windows 用 cmd /c，其他系统用 /bin/sh -c。
/// </summary>
internal static class ShellCommand
{
    /// <summary>
    /// 创建不显示窗口、不重定向输出的进程信息。
    /// </summary>
    /// <param name="command">整行命令，原样交给 shell 解释。</param>
    /// <param name="workingDirectory">工作目录。</param>
    /// <param name="environment">在继承的环境变量之上额外设置的变量。</param>
    public static ProcessStartInfo CreateStartInfo(
        string command,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        ProcessStartInfo startInfo;
        if (PlatformInfo.Os == OsKind.Windows)
        {
            // /s 让 cmd 只去掉最外层的一对引号，命令里的引号原样保留；/d 跳过注册表中的 AutoRun
            startInfo = new ProcessStartInfo("cmd.exe", $"/d /s /c \"{command}\"");
        }
        else
        {
            startInfo = new ProcessStartInfo("/bin/sh");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
        }

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        foreach (var (name, value) in environment)
            startInfo.Environment[name] = value;
        return startInfo;
    }
}
