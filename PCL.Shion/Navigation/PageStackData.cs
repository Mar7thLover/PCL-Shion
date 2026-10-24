using System;
using System.Collections.Generic;

namespace PCL.Shion.Navigation;

/// <summary>
///     页面堆栈中的一个主页面。用于记录返回时需要恢复的页面。
/// </summary>
public class PageStackData
{
    public PageType page;

    public override bool Equals(object? other)
    {
        if (other is null)
            return false;
        if (other is PageStackData pageOther)
            return page == pageOther.page;
        return false;
    }

    public override int GetHashCode() => (int)page;

    public static bool operator ==(PageStackData? left, PageStackData? right)
    {
        return EqualityComparer<PageStackData>.Default.Equals(left!, right!);
    }

    public static bool operator !=(PageStackData? left, PageStackData? right)
    {
        return !(left == right);
    }

    public static implicit operator PageStackData(PageType value)
    {
        return new PageStackData { page = value };
    }

    public static implicit operator PageType(PageStackData value)
    {
        return value.page;
    }

    public override string ToString() => page.ToString();
}
