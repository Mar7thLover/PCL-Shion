using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using PCL.Shion.Core.Download;
using PCL.Shion.Core.Platform;

namespace PCL.Shion.Core.Loaders.Forge;

/// <summary>
/// 用独立的 Java 进程运行安装处理器。
/// </summary>
internal static class ProcessorRunner
{
    private const string MainClassHeader = "Main-Class:";

    /// <summary>失败时保留的输出行数。</summary>
    private const int OutputLimit = 10;

    /// <summary>
    /// 运行处理器并校验它的输出。声明了输出、且输出全部已存在并通过校验时不再运行，与官方安装器一致。
    /// </summary>
    /// <param name="workingDirectory">进程的工作目录，处理器写到相对路径的文件会落在这里。</param>
    /// <exception cref="ForgeProcessorException">进程的退出码不为 0。</exception>
    /// <exception cref="InvalidDataException">处理器 jar 没有声明主类，或输出文件没有通过校验。</exception>
    /// <exception cref="System.ComponentModel.Win32Exception">无法启动 Java。</exception>
    public static async Task RunAsync(
        PreparedProcessor processor,
        string javaExecutable,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (processor.Outputs.Count > 0 && processor.Outputs.All(file => FileVerifier.Matches(file.Key, null, file.Value)))
            return;
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(javaExecutable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in CreateArguments(processor, ReadMainClass(processor.JarPath)))
            startInfo.ArgumentList.Add(argument);

        var output = new Queue<string>();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Collect(output, e.Data);
        process.ErrorDataReceived += (_, e) => Collect(output, e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 等进程真正退出，它打开的文件才会释放，临时目录才能删掉
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw;
        }

        if (process.ExitCode != 0)
        {
            string[] lines;
            lock (output)
                lines = output.ToArray();
            throw new ForgeProcessorException(processor.Jar, process.ExitCode, lines);
        }
        VerifyOutputs(processor);
    }

    /// <summary>
    /// Java 的命令行参数：输出编码、classpath（处理器 jar 在最前）、主类，然后是处理器的参数。
    /// </summary>
    internal static IReadOnlyList<string> CreateArguments(PreparedProcessor processor, string mainClass)
    {
        return
        [
            "-Dstdout.encoding=UTF-8",
            "-Dstderr.encoding=UTF-8",
            "-cp",
            string.Join(PlatformInfo.ClasspathSeparator, processor.Classpath.Prepend(processor.JarPath)),
            mainClass,
            .. processor.Arguments
        ];
    }

    /// <summary>
    /// 从 jar 清单的文本中读出 Main-Class；清单的长行会折行，续行以一个空格开头。没有声明时返回 null。
    /// </summary>
    internal static string? ParseMainClass(string manifest)
    {
        var lines = manifest.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith(MainClassHeader, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = new StringBuilder(lines[i][MainClassHeader.Length..].TrimStart());
            while (i + 1 < lines.Length && lines[i + 1].StartsWith(' '))
                value.Append(lines[++i].AsSpan(1));
            return value.ToString().TrimEnd();
        }
        return null;
    }

    private static string ReadMainClass(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var manifest = archive.GetEntry("META-INF/MANIFEST.MF")
            ?? throw new InvalidDataException($"The installer processor {jarPath} has no manifest.");
        using var reader = new StreamReader(manifest.Open());
        return ParseMainClass(reader.ReadToEnd())
            ?? throw new InvalidDataException($"The manifest of the installer processor {jarPath} declares no Main-Class.");
    }

    private static void Collect(Queue<string> output, string? line)
    {
        if (line is null)
            return;

        lock (output)
        {
            output.Enqueue(line);
            if (output.Count > OutputLimit)
                output.Dequeue();
        }
    }

    // 删掉不合格的输出，免得它留在 libraries 里被当成有效文件
    private static void VerifyOutputs(PreparedProcessor processor)
    {
        foreach (var (path, sha1) in processor.Outputs)
        {
            if (FileVerifier.Matches(path, null, sha1))
                continue;

            File.Delete(path);
            throw new InvalidDataException($"The installer processor {processor.Jar} did not produce {path} with SHA-1 {sha1}.");
        }
    }
}
