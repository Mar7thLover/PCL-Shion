using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Shion.Core.Worlds.Nbt;
using static PCL.Shion.Core.Test.NbtBytes;

namespace PCL.Shion.Core.Test;

[TestClass]
public sealed class NbtReaderTest
{
    [TestMethod]
    public void ReadsEveryTagType()
    {
        var data = Root(
            Named(NbtTagType.Byte, "byte", ByteValue(-2)),
            Named(NbtTagType.Short, "short", ShortValue(0x0102)),
            Named(NbtTagType.Int, "int", IntValue(-0x01020304)),
            Named(NbtTagType.Long, "long", LongValue(0x0102030405060708)),
            Named(NbtTagType.Float, "float", FloatValue(1.5f)),
            Named(NbtTagType.Double, "double", DoubleValue(-2.25)),
            Named(NbtTagType.ByteArray, "bytes", ByteArrayValue(1, 2, 255)),
            Named(NbtTagType.String, "string", StringValue("hello")),
            Named(NbtTagType.List, "list", ListValue(NbtTagType.Int, IntValue(7), IntValue(8))),
            Named(NbtTagType.Compound, "compound", CompoundValue(Named(NbtTagType.Int, "inner", IntValue(9)))),
            Named(NbtTagType.IntArray, "ints", IntArrayValue(1, -1)),
            Named(NbtTagType.LongArray, "longs", LongArrayValue(long.MinValue, 2)));

        var root = NbtReader.Read(data);

        Assert.AreEqual(NbtTagType.Compound, root.Type);
        Assert.HasCount(12, root.Entries);
        Assert.AreEqual((sbyte)-2, ValueOf<sbyte>(root, "byte", NbtTagType.Byte));
        Assert.AreEqual((short)0x0102, ValueOf<short>(root, "short", NbtTagType.Short));
        Assert.AreEqual(-0x01020304, ValueOf<int>(root, "int", NbtTagType.Int));
        Assert.AreEqual(0x0102030405060708, ValueOf<long>(root, "long", NbtTagType.Long));
        Assert.AreEqual(1.5f, ValueOf<float>(root, "float", NbtTagType.Float));
        Assert.AreEqual(-2.25, ValueOf<double>(root, "double", NbtTagType.Double));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 255 }, ValueOf<byte[]>(root, "bytes", NbtTagType.ByteArray));
        Assert.AreEqual("hello", ValueOf<string>(root, "string", NbtTagType.String));
        CollectionAssert.AreEqual(new[] { 1, -1 }, ValueOf<int[]>(root, "ints", NbtTagType.IntArray));
        CollectionAssert.AreEqual(new[] { long.MinValue, 2 }, ValueOf<long[]>(root, "longs", NbtTagType.LongArray));

        var list = root.GetList("list")!;
        Assert.AreEqual(NbtTagType.List, list.Type);
        Assert.AreEqual(NbtTagType.Int, list.ElementType);
        CollectionAssert.AreEqual(new[] { 7, 8 }, list.Select(item => ((NbtValue<int>)item).Value).ToArray());

        Assert.AreEqual(9, ValueOf<int>(root.GetCompound("compound")!, "inner", NbtTagType.Int));
    }

    [TestMethod]
    public void ReadsEmptyListsAndListsOfContainers()
    {
        var data = Root(
            Named(NbtTagType.List, "empty", ListValue(NbtTagType.End)),
            Named(NbtTagType.List, "compounds", ListValue(
                NbtTagType.Compound,
                CompoundValue(Named(NbtTagType.String, "id", StringValue("first"))),
                CompoundValue(Named(NbtTagType.String, "id", StringValue("second"))))),
            Named(NbtTagType.List, "lists", ListValue(
                NbtTagType.List,
                ListValue(NbtTagType.Byte, ByteValue(1)),
                ListValue(NbtTagType.Byte))));

        var root = NbtReader.Read(data);

        var empty = root.GetList("empty")!;
        Assert.HasCount(0, empty);
        Assert.AreEqual(NbtTagType.End, empty.ElementType);

        var compounds = root.GetList("compounds")!;
        CollectionAssert.AreEqual(
            new[] { "first", "second" },
            compounds.Cast<NbtCompound>().Select(compound => compound.GetString("id")).ToArray());

        var lists = root.GetList("lists")!;
        Assert.HasCount(2, lists);
        Assert.HasCount(1, (NbtList)lists[0]);
        Assert.HasCount(0, (NbtList)lists[1]);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("ascii text")]
    [DataRow("café")]
    [DataRow("世界")]
    [DataRow("nul\0char")]
    [DataRow("\U0001F30D earth")]
    public void DecodesStringsWrittenAsModifiedUtf8(string text)
    {
        var data = Root(Named(NbtTagType.String, "text", StringValue(text)));

        Assert.AreEqual(text, NbtReader.Read(data).GetString("text"));
    }

    [TestMethod]
    public void DecodesModifiedUtf8ByteSequences()
    {
        var data = Root(
            Named(NbtTagType.String, "nul", [0x00, 0x02, 0xC0, 0x80]),
            Named(NbtTagType.String, "pair", [0x00, 0x06, 0xED, 0xA0, 0xBC, 0xED, 0xBC, 0x8D]));

        var root = NbtReader.Read(data);

        Assert.AreEqual("\0", root.GetString("nul"));
        Assert.AreEqual("\U0001F30D", root.GetString("pair"));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x00, 0x04, 0xF0, 0x9F, 0x8C, 0x8D })]
    [DataRow(new byte[] { 0x00, 0x01, 0xC3 })]
    [DataRow(new byte[] { 0x00, 0x02, 0xE4, 0xB8 })]
    [DataRow(new byte[] { 0x00, 0x02, 0xC3, 0x41 })]
    [DataRow(new byte[] { 0x00, 0x01, 0x80 })]
    public void RejectsInvalidModifiedUtf8(byte[] stringPayload)
    {
        var data = Root(Named(NbtTagType.String, "text", stringPayload));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(data));
    }

    [TestMethod]
    public void LaterEntryWithTheSameNameWins()
    {
        var data = Root(
            Named(NbtTagType.Int, "value", IntValue(1)),
            Named(NbtTagType.Int, "value", IntValue(2)));

        Assert.AreEqual(2L, NbtReader.Read(data).GetLong("value"));
    }

    [TestMethod]
    public void TypedAccessorsReturnNullForMissingOrMismatchedTags()
    {
        var data = Root(
            Named(NbtTagType.String, "text", StringValue("x")),
            Named(NbtTagType.Int, "number", IntValue(5)),
            Named(NbtTagType.Compound, "compound", CompoundValue()),
            Named(NbtTagType.List, "list", ListValue(NbtTagType.End)));

        var root = NbtReader.Read(data);

        Assert.IsNull(root.Get("missing"));
        Assert.IsNull(root.GetString("missing"));
        Assert.IsNull(root.GetString("number"));
        Assert.IsNull(root.GetLong("text"));
        Assert.IsNull(root.GetLong("missing"));
        Assert.IsNull(root.GetCompound("list"));
        Assert.IsNull(root.GetList("compound"));
    }

    [TestMethod]
    public void GetLongWidensEveryIntegerTagType()
    {
        var data = Root(
            Named(NbtTagType.Byte, "byte", ByteValue(-3)),
            Named(NbtTagType.Short, "short", ShortValue(-300)),
            Named(NbtTagType.Int, "int", IntValue(-70000)),
            Named(NbtTagType.Long, "long", LongValue(-5_000_000_000)),
            Named(NbtTagType.Float, "float", FloatValue(1f)));

        var root = NbtReader.Read(data);

        Assert.AreEqual(-3L, root.GetLong("byte"));
        Assert.AreEqual(-300L, root.GetLong("short"));
        Assert.AreEqual(-70000L, root.GetLong("int"));
        Assert.AreEqual(-5_000_000_000, root.GetLong("long"));
        Assert.IsNull(root.GetLong("float"));
    }

    [TestMethod]
    public void ReadsUncompressedAndGZipStreamsWithoutClosingThem()
    {
        var data = Root(Named(NbtTagType.Int, "value", IntValue(42)));
        using var plain = new MemoryStream(data);
        using var compressed = new MemoryStream(GZip(data));

        Assert.AreEqual(42L, NbtReader.Read(plain).GetLong("value"));
        Assert.AreEqual(42L, NbtReader.ReadGZip(compressed).GetLong("value"));
        Assert.IsTrue(plain.CanRead);
        Assert.IsTrue(compressed.CanRead);
    }

    [TestMethod]
    public void RejectsUncompressedDataWhereGZipIsExpected()
    {
        using var stream = new MemoryStream(Root(Named(NbtTagType.Int, "value", IntValue(1))));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.ReadGZip(stream));
    }

    [TestMethod]
    public void RejectsGZipDataThatExpandsBeyondTheLimit()
    {
        using var stream = new MemoryStream(GZip(new byte[17 * 1024 * 1024]));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.ReadGZip(stream));
    }

    [TestMethod]
    public void RejectsRootThatIsNotACompound()
    {
        var data = Named(NbtTagType.Int, "", IntValue(1));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(data));
    }

    [TestMethod]
    public void RejectsEmptyAndTruncatedData()
    {
        var data = Root(
            Named(NbtTagType.String, "text", StringValue("hello")),
            Named(NbtTagType.Long, "long", LongValue(1)));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(Array.Empty<byte>()));
        for (var length = 1; length < data.Length; length++)
        {
            var truncated = data[..length];
            Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(truncated), $"length {length}");
        }
    }

    [TestMethod]
    public void RejectsUnknownTagTypes()
    {
        var data = Root(Named((NbtTagType)13, "x", []));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(data));
    }

    [TestMethod]
    public void RejectsNonEmptyListsOfEndTags()
    {
        var data = Root(Named(NbtTagType.List, "list", ListValue(NbtTagType.End, Array.Empty<byte>())));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(data));
    }

    [TestMethod]
    public void RejectsNegativeLengths()
    {
        var byteArray = Root(Named(NbtTagType.ByteArray, "bytes", IntValue(-1)));
        var list = Root(Named(NbtTagType.List, "list", [(byte)NbtTagType.Int, .. IntValue(-1)]));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(byteArray));
        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(list));
    }

    [TestMethod]
    public void RejectsLengthsLargerThanTheRemainingData()
    {
        var byteArray = Root(Named(NbtTagType.ByteArray, "bytes", [.. IntValue(100), 1, 2, 3]));
        var intArray = Root(Named(NbtTagType.IntArray, "ints", [.. IntValue(int.MaxValue), 0, 0, 0, 0]));
        var longArray = Root(Named(NbtTagType.LongArray, "longs", [.. IntValue(3), .. LongValue(1), .. LongValue(2)]));
        var list = Root(Named(NbtTagType.List, "list", [(byte)NbtTagType.Byte, .. IntValue(int.MaxValue)]));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(byteArray));
        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(intArray));
        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(longArray));
        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(list));
    }

    [TestMethod]
    public void AcceptsNestingUpToTheLimitAndRejectsDeeperCompounds()
    {
        Assert.IsNotNull(NbtReader.Read(NestedCompounds(512)));
        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(NestedCompounds(513)));
    }

    [TestMethod]
    public void RejectsDeeplyNestedLists()
    {
        var payload = ListValue(NbtTagType.End);
        for (var level = 0; level < 600; level++)
            payload = ListValue(NbtTagType.List, payload);
        var data = Root(Named(NbtTagType.List, "list", payload));

        Assert.ThrowsExactly<InvalidDataException>(() => NbtReader.Read(data));
    }

    private static T ValueOf<T>(NbtCompound compound, string name, NbtTagType type)
    {
        var tag = compound.Get(name);
        Assert.IsNotNull(tag);
        Assert.AreEqual(type, tag.Type);
        return ((NbtValue<T>)tag).Value;
    }

    /// <summary>
    /// 生成共 <paramref name="depth"/> 层 Compound 嵌套的数据，根标签算第一层。
    /// </summary>
    private static byte[] NestedCompounds(int depth)
    {
        var payload = CompoundValue();
        for (var level = 1; level < depth; level++)
            payload = CompoundValue(Named(NbtTagType.Compound, "c", payload));
        return Named(NbtTagType.Compound, "", payload);
    }
}
