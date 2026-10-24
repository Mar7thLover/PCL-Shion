using System.Collections;

namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// List 标签：元素类型相同的有序序列。
/// </summary>
public sealed class NbtList : NbtTag, IReadOnlyList<NbtTag>
{
    private readonly List<NbtTag> _items;

    internal NbtList(NbtTagType elementType, List<NbtTag> items) : base(NbtTagType.List)
    {
        ElementType = elementType;
        _items = items;
    }

    /// <summary>
    /// 元素类型；空列表在文件中可能记为 <see cref="NbtTagType.End"/>。
    /// </summary>
    public NbtTagType ElementType { get; }

    public int Count => _items.Count;

    public NbtTag this[int index] => _items[index];

    public IEnumerator<NbtTag> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
