using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Mods;

namespace PCL.Shion.Core.Test.Mods;

[TestClass]
public sealed class TomlReaderTest
{
    [TestMethod]
    public void Parse_ReadsScalarValues()
    {
        var table = TomlReader.Parse("""
            basic = "tab\there \"quoted\" \\ \u00e9 \U0001F600"
            literal = 'C:\path\no\escapes'
            empty = ""
            integer = 1_000
            negative = -7
            real = 1.5
            yes = true
            no = false
            """);

        Assert.AreEqual("tab\there \"quoted\" \\ \u00e9 " + char.ConvertFromUtf32(0x1F600), table.GetString("basic"));
        Assert.AreEqual(@"C:\path\no\escapes", table.GetString("literal"));
        Assert.AreEqual(string.Empty, table.GetString("empty"));
        Assert.AreEqual(1000L, (long)table["integer"]);
        Assert.AreEqual(-7L, (long)table["negative"]);
        Assert.AreEqual(1.5, (double)table["real"]);
        Assert.AreEqual(true, table.GetBool("yes"));
        Assert.AreEqual(false, table.GetBool("no"));
    }

    [TestMethod]
    public void Parse_ReadsMultilineStrings()
    {
        var table = TomlReader.Parse(""""
            literal = '''
            line one
            it's "quoted"
            line three'''
            basic = """
            joined \
                lines, and "" quotes"""
            """");

        Assert.AreEqual("line one\nit's \"quoted\"\nline three", table.GetString("literal")?.ReplaceLineEndings("\n"));
        Assert.AreEqual("joined lines, and \"\" quotes", table.GetString("basic"));
    }

    [TestMethod]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var table = TomlReader.Parse("""
            # leading comment

            name = "value" # trailing comment
            hash = "not # a comment"

            # [not.a.table]
            """);

        Assert.HasCount(2, table);
        Assert.AreEqual("value", table.GetString("name"));
        Assert.AreEqual("not # a comment", table.GetString("hash"));
    }

    [TestMethod]
    public void Parse_AcceptsCrLfLineEndings()
    {
        var table = TomlReader.Parse("a = 1 # one\r\n[section]\r\nb = \"two\"\r\n\r\n[[items]]\r\nc = 3\r\n");

        Assert.AreEqual(1L, (long)table["a"]);
        Assert.AreEqual("two", table.GetTable("section")?.GetString("b"));
        Assert.HasCount(1, table.GetTables("items"));
    }

    [TestMethod]
    public void Parse_ReadsTablesAndDottedKeys()
    {
        var table = TomlReader.Parse("""
            title = "root"
            a.b.c = true

            [server]
            host = "localhost"

            [server.limits]
            max = 5

            [ "quoted key" . plain ]
            value = 'x'
            """);

        Assert.AreEqual("root", table.GetString("title"));
        Assert.AreEqual(true, table.GetTable("a")?.GetTable("b")?.GetBool("c"));
        Assert.AreEqual("localhost", table.GetTable("server")?.GetString("host"));
        Assert.AreEqual(5L, (long)table.GetTable("server")!.GetTable("limits")!["max"]);
        Assert.AreEqual("x", table.GetTable("quoted key")?.GetTable("plain")?.GetString("value"));
    }

    [TestMethod]
    public void Parse_ReadsArrayOfTables()
    {
        var table = TomlReader.Parse("""
            [[mods]]
            modId = "one"

            [[mods]]
            modId = "two"

            [[dependencies.one]]
            modId = "forge"

            [[dependencies.one]]
            modId = "minecraft"
            """);

        var mods = table.GetTables("mods");
        Assert.HasCount(2, mods);
        Assert.AreEqual("one", mods[0].GetString("modId"));
        Assert.AreEqual("two", mods[1].GetString("modId"));

        var dependencies = table.GetTable("dependencies")!.GetTables("one");
        Assert.HasCount(2, dependencies);
        Assert.AreEqual("minecraft", dependencies[1].GetString("modId"));
        Assert.IsEmpty(table.GetTable("dependencies")!.GetTables("two"));
    }

    [TestMethod]
    public void Parse_AttachesSubTablesToTheLatestArrayElement()
    {
        var table = TomlReader.Parse("""
            [[fruits]]
            name = "apple"

            [fruits.physical]
            color = "red"

            [[fruits]]
            name = "banana"
            """);

        var fruits = table.GetTables("fruits");
        Assert.HasCount(2, fruits);
        Assert.AreEqual("red", fruits[0].GetTable("physical")?.GetString("color"));
        Assert.IsNull(fruits[1].GetTable("physical"));
    }

    [TestMethod]
    public void Parse_ReadsArraysAndInlineTables()
    {
        var table = TomlReader.Parse("""
            authors = [ "a", 'b',  # comment
                "c",
            ]
            nested = [[1, 2], [3]]
            point = { x = 1, y = "two", inner = { z = false } }
            empty = []
            """);

        CollectionAssert.AreEqual(new object[] { "a", "b", "c" }, (List<object>)table["authors"]);
        var nested = (List<object>)table["nested"];
        Assert.HasCount(2, nested);
        CollectionAssert.AreEqual(new object[] { 1L, 2L }, (List<object>)nested[0]);

        var point = table.GetTable("point")!;
        Assert.AreEqual(1L, (long)point["x"]);
        Assert.AreEqual("two", point.GetString("y"));
        Assert.AreEqual(false, point.GetTable("inner")?.GetBool("z"));
        Assert.IsEmpty((List<object>)table["empty"]);
    }

    [TestMethod]
    public void Parse_ReadsTypicalModsToml()
    {
        var table = TomlReader.Parse("""
            modLoader="javafml" #mandatory
            loaderVersion="[47,)" #mandatory
            license="MIT"

            [[mods]] #mandatory
            modId="examplemod" #mandatory
            version="${file.jarVersion}"
            displayName="Example Mod"
            description='''
            An example mod.
            '''

            [[dependencies.examplemod]]
                modId="forge"
                mandatory=true
                versionRange="[47,)"
                ordering="NONE"
                side="BOTH"
            """);

        Assert.AreEqual("javafml", table.GetString("modLoader"));
        var mod = table.GetTables("mods").Single();
        Assert.AreEqual("${file.jarVersion}", mod.GetString("version"));
        Assert.AreEqual("An example mod.\n", mod.GetString("description")?.ReplaceLineEndings("\n"));
        var dependency = table.GetTable("dependencies")!.GetTables("examplemod").Single();
        Assert.AreEqual(true, dependency.GetBool("mandatory"));
        Assert.AreEqual("[47,)", dependency.GetString("versionRange"));
    }

    [TestMethod]
    public void Getters_ReturnNullOrEmptyForMissingOrMismatchedValues()
    {
        var table = TomlReader.Parse("""
            text = "value"
            number = 1
            """);

        Assert.IsNull(table.GetString("number"));
        Assert.IsNull(table.GetBool("text"));
        Assert.IsNull(table.GetTable("text"));
        Assert.IsNull(table.GetString("missing"));
        Assert.IsEmpty(table.GetTables("text"));
    }

    [TestMethod]
    [DataRow("name = ")]
    [DataRow("name 1")]
    [DataRow("name = \"unterminated")]
    [DataRow("name = \"line\nbreak\"")]
    [DataRow("name = \"bad \\q escape\"")]
    [DataRow("name = \"bad \\uZZZZ escape\"")]
    [DataRow("a = 1\na = 2")]
    [DataRow("a = 1 b = 2")]
    [DataRow("a = [1, 2")]
    [DataRow("a = { x = 1")]
    [DataRow("[table")]
    [DataRow("a = 1\n[a]")]
    [DataRow("a = 1\n[[a]]")]
    public void Parse_ThrowsForInvalidSyntax(string text)
    {
        Assert.ThrowsExactly<FormatException>(() => TomlReader.Parse(text));
    }

    [TestMethod]
    public void Parse_ReportsTheLineOfTheError()
    {
        var exception = Assert.ThrowsExactly<FormatException>(() => TomlReader.Parse("a = 1\nb = \"x"));

        Assert.Contains("line 2", exception.Message);
    }
}
