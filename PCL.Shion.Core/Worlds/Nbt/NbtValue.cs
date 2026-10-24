namespace PCL.Shion.Core.Worlds.Nbt;

/// <summary>
/// 承载单个值的标签。值的类型随标签类型而定：
/// Byte 为 <see cref="sbyte"/>，Short 为 <see cref="short"/>，Int 为 <see cref="int"/>，Long 为 <see cref="long"/>，
/// Float 为 <see cref="float"/>，Double 为 <see cref="double"/>，String 为 <see cref="string"/>，
/// ByteArray 为 <see cref="byte"/>[]，IntArray 为 <see cref="int"/>[]，LongArray 为 <see cref="long"/>[]。
/// </summary>
/// <typeparam name="T">值的类型。</typeparam>
public sealed class NbtValue<T> : NbtTag
{
    internal NbtValue(NbtTagType type, T value) : base(type)
    {
        Value = value;
    }

    public T Value { get; }
}
