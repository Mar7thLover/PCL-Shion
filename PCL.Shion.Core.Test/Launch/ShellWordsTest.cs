using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Launch;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class ShellWordsTest
{
    [TestMethod]
    [DataRow("prime-run", new[] { "prime-run" })]
    [DataRow("  taskset   -c 0-3 ", new[] { "taskset", "-c", "0-3" })]
    [DataRow("   ", new string[0])]
    [DataRow("""'a b' "c d" e\ f""", new[] { "a b", "c d", "e f" })]
    [DataRow("""'single "quoted" $HOME \n'""", new[] { """single "quoted" $HOME \n""" })]
    [DataRow("""say "\"hi\" \$HOME \\ \n" """, new[] { "say", "\"hi\" $HOME \\ \\n" })]
    [DataRow("""-Dkey="a b"c""", new[] { "-Dkey=a bc" })]
    [DataRow("""'' "" x""", new[] { "", "", "x" })]
    [DataRow("""unterminated "quote here""", new[] { "unterminated", "quote here" })]
    public void SplitsLikeAPosixShell(string text, string[] expected)
    {
        CollectionAssert.AreEqual(expected, ShellWords.SplitPosix(text).ToArray());
    }

    [TestMethod]
    [DataRow("""C:\Tools\wrap.exe --flag""", new[] { """C:\Tools\wrap.exe""", "--flag" })]
    [DataRow(""" "C:\Program Files\Wrap\wrap.exe"  -a """, new[] { """C:\Program Files\Wrap\wrap.exe""", "-a" })]
    [DataRow("""-Dpath="C:\My Dir" -Xmx4G""", new[] { """-Dpath=C:\My Dir""", "-Xmx4G" })]
    [DataRow("""a\\\"b c""", new[] { """a\"b""", "c" })]
    [DataRow("""a\\"b c" d""", new[] { """a\b c""", "d" })]
    [DataRow("""'single' "" x""", new[] { "'single'", "", "x" })]
    [DataRow("""unterminated "quote here""", new[] { "unterminated", "quote here" })]
    public void SplitsLikeAWindowsCommandLine(string text, string[] expected)
    {
        CollectionAssert.AreEqual(expected, ShellWords.SplitWindows(text).ToArray());
    }
}
