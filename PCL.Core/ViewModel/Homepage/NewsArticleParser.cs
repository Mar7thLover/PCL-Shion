using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace PCL.Core.ViewModel.Homepage;

/// <summary>
/// Minecraft 新闻正文块的种类。
/// </summary>
public enum NewsArticleBlockKind
{
    /// <summary>标题，<see cref="NewsArticleBlock.Level"/> 为 1 到 6。</summary>
    Heading,

    /// <summary>普通段落。</summary>
    Paragraph,

    /// <summary>
    /// 列表项。<see cref="NewsArticleBlock.Level"/> 为嵌套层数（从 1 开始），
    /// 有序列表项的 <see cref="NewsArticleBlock.Index"/> 为序号（从 1 开始），无序列表项为 0。
    /// </summary>
    ListItem,

    /// <summary>图片。<see cref="NewsArticleBlock.Url"/> 为绝对地址，<see cref="NewsArticleBlock.Text"/> 为替代文本。</summary>
    Image,

    /// <summary>图片说明。</summary>
    Caption
}

/// <summary>
/// 正文块文本里的一段链接。<see cref="Start"/> 与 <see cref="Length"/> 指向 <see cref="NewsArticleBlock.Text"/>。
/// </summary>
/// <param name="Start">链接文字在块文本中的起始位置。</param>
/// <param name="Length">链接文字的长度。</param>
/// <param name="Url">链接的绝对地址，只会是 http 或 https。</param>
public sealed record NewsArticleLink(int Start, int Length, string Url);

/// <summary>
/// Minecraft 新闻正文里的一个块。
/// </summary>
public sealed record NewsArticleBlock
{
    public required NewsArticleBlockKind Kind { get; init; }

    /// <summary>
    /// 块的文本：已解码 HTML 实体、合并空白。段落内的换行（br）以 <c>\n</c> 表示。
    /// 图片块则是图片的替代文本，可能为空。
    /// </summary>
    public string Text { get; init; } = "";

    /// <summary>标题级别（1 到 6），或列表项的嵌套层数（从 1 开始）。其他块为 0。</summary>
    public int Level { get; init; }

    /// <summary>有序列表项的序号（从 1 开始）。其他块，以及无序列表项为 0。</summary>
    public int Index { get; init; }

    /// <summary>图片的绝对地址。仅图片块有值。</summary>
    public string? Url { get; init; }

    /// <summary>文本中的链接，按出现顺序排列，互不重叠。</summary>
    public IReadOnlyList<NewsArticleLink> Links { get; init; } = Array.Empty<NewsArticleLink>();
}

/// <summary>
/// 从 minecraft.net 新闻文章页面的 HTML 里取出正文，得到按文档顺序排列的块列表。
/// 只处理文章网格（MC_articleGridA）内的内容，页眉、导航、分享、页脚等都会被忽略。
/// 这是一个宽容的扫描器，不依赖第三方 HTML 解析库；遇到无法识别的内容时返回空列表而不是抛出异常。
/// </summary>
public static class NewsArticleParser
{
    /// <summary>
    /// 没有提供有效基准地址时，用于补全站内相对地址。
    /// </summary>
    public const string DefaultBaseUrl = "https://www.minecraft.net/";

    private const string _GridMarker = "MC_articleGridA";

    /// <summary>
    /// 文章正文之后的内容的标记，用于确定正文的结束位置。
    /// </summary>
    private static readonly string[] _EndMarkers = ["MC_socialShareA", "MC_globalFooterA", "<footer"];

    /// <summary>
    /// 解析文章页面。
    /// </summary>
    /// <param name="html">文章页面的完整 HTML。</param>
    /// <param name="baseUrl">页面地址，用于把图片、链接的相对地址补全为绝对地址。无效时使用 <see cref="DefaultBaseUrl"/>。</param>
    /// <returns>正文块列表。没有找到正文时返回空列表。</returns>
    public static IReadOnlyList<NewsArticleBlock> Parse(string? html, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        if (!_TryFindRegion(html, out var start, out var end)) return [];
        return new Extractor(html, start, end, _ResolveBaseUri(baseUrl)).Run();
    }

    private static Uri _ResolveBaseUri(string? baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri;
        }

        return new Uri(DefaultBaseUrl);
    }

    /// <summary>
    /// 找出正文所在的范围：从第一个文章网格的起始标签，到分享区块、页脚或“最新新闻”区块之前。
    /// </summary>
    private static bool _TryFindRegion(string html, out int start, out int end)
    {
        start = 0;
        end = html.Length;

        var firstGrid = _IndexOfInTag(html, _GridMarker, 0);
        if (firstGrid < 0) return false;
        start = _TagStart(html, firstGrid, 0);

        foreach (var marker in _EndMarkers)
        {
            var index = html.IndexOf(marker, start, StringComparison.Ordinal);
            if (index >= 0) end = Math.Min(end, _TagStart(html, index, start));
        }

        // 页尾“最新新闻”列表同样使用图片网格；只有在最后一个文章网格之后出现时，才视为正文已经结束
        var lastGrid = _LastIndexOfInTag(html, _GridMarker);
        var gallery = html.IndexOf("MC_imageGridA", Math.Max(start, lastGrid), StringComparison.Ordinal);
        if (gallery >= 0) end = Math.Min(end, _TagStart(html, gallery, start));

        return end > start;
    }

    /// <summary>
    /// 找到标记在标签内（例如 class 属性里）的第一处出现位置，忽略样式和脚本文本里的同名字符串。
    /// </summary>
    private static int _IndexOfInTag(string html, string marker, int from)
    {
        var index = html.IndexOf(marker, from, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (_IsInsideTag(html, index)) return index;
            index = html.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
        }

        return -1;
    }

    private static int _LastIndexOfInTag(string html, string marker)
    {
        var index = html.LastIndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (_IsInsideTag(html, index)) return index;
            if (index == 0) break;
            index = html.LastIndexOf(marker, index - 1, StringComparison.Ordinal);
        }

        return -1;
    }

    private static bool _IsInsideTag(string html, int index)
    {
        var open = html.LastIndexOf('<', index);
        if (open < 0 || open + 1 >= html.Length || !char.IsAsciiLetter(html[open + 1])) return false;
        return html.LastIndexOf('>', index) < open;
    }

    private static int _TagStart(string html, int index, int lowerBound)
    {
        var open = html.LastIndexOf('<', index);
        return open < lowerBound ? index : open;
    }

    /// <summary>
    /// 在指定范围内逐个扫描标签和文本，累积出正文块。
    /// </summary>
    private sealed class Extractor
    {
        /// <summary>整个元素（含子元素）都不属于正文的容器。script 和 style 由原始文本规则单独处理。</summary>
        private static readonly HashSet<string> _SkippedContainers = new(StringComparer.Ordinal)
        {
            "noscript", "template", "svg", "nav", "button", "form", "select", "iframe", "object", "canvas", "video",
            "audio", "dialog", "head"
        };

        /// <summary>遇到这些标签的开始或结束时，把已经累积的文本作为一个块输出。</summary>
        private static readonly HashSet<string> _BlockTags = new(StringComparer.Ordinal)
        {
            "p", "div", "section", "article", "aside", "header", "footer", "main", "blockquote", "pre", "table",
            "thead", "tbody", "tfoot", "tr", "caption", "figure", "picture", "dl", "dt", "dd", "hr", "details",
            "summary", "address", "fieldset", "center"
        };

        private static readonly string[] _ImageSourceKeys = ["src", "data-src", "data-lazy-src", "data-original"];

        private readonly string _html;
        private readonly int _start;
        private readonly int _end;
        private readonly Uri _baseUri;

        private readonly List<NewsArticleBlock> _blocks = [];
        private readonly StringBuilder _text = new();
        private readonly List<NewsArticleLink> _links = [];
        private readonly List<ListState> _lists = [];

        // 下一个输出的文本块的种类。输出一个块后恢复为普通段落
        private NewsArticleBlockKind _kind = NewsArticleBlockKind.Paragraph;
        private int _level;
        private int _index;

        // 当前打开的链接
        private string? _linkUrl;
        private int _linkStart;

        // 正在跳过的容器
        private string? _skipTag;
        private int _skipDepth;

        public Extractor(string html, int start, int end, Uri baseUri)
        {
            _html = html;
            _start = start;
            _end = end;
            _baseUri = baseUri;
        }

        private sealed class ListState(bool ordered, int counter)
        {
            public bool Ordered { get; } = ordered;
            public int Counter { get; set; } = counter;
        }

        public List<NewsArticleBlock> Run()
        {
            var pos = _start;
            while (pos < _end)
            {
                var lt = _html.IndexOf('<', pos, _end - pos);
                if (lt < 0)
                {
                    _AppendText(pos, _end);
                    break;
                }

                if (lt > pos) _AppendText(pos, lt);
                pos = _HandleMarkup(lt);
            }

            _Flush();
            return _blocks;
        }

        #region 标签扫描

        /// <summary>
        /// 处理位于 <paramref name="lt"/> 的 '&lt;'，返回下一个扫描位置。
        /// </summary>
        private int _HandleMarkup(int lt)
        {
            if (string.CompareOrdinal(_html, lt, "<!--", 0, 4) == 0)
            {
                if (lt + 4 > _end) return _end;
                var close = _html.IndexOf("-->", lt + 4, _end - lt - 4, StringComparison.Ordinal);
                return close < 0 ? _end : close + 3;
            }

            var next = lt + 1 < _end ? _html[lt + 1] : '\0';
            if (next is '!' or '?')
            {
                // DOCTYPE、CDATA、处理指令
                var bang = _html.IndexOf('>', lt, _end - lt);
                return bang < 0 ? _end : bang + 1;
            }

            var isEnd = next == '/';
            var nameStart = isEnd ? lt + 2 : lt + 1;
            if (nameStart >= _end || !char.IsAsciiLetter(_html[nameStart]))
            {
                // 不是标签，按普通文本处理
                _AppendChar('<');
                return lt + 1;
            }

            var nameEnd = nameStart + 1;
            while (nameEnd < _end && _IsNameChar(_html[nameEnd])) nameEnd++;
            var tagEnd = _FindTagEnd(nameEnd);
            if (tagEnd < 0) return _end; // 标签没有结束，后面的内容无法可靠地解析

            var name = _html.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
            var selfClosing = _html[tagEnd - 1] == '/';
            _HandleTag(name, isEnd, nameEnd, tagEnd, selfClosing);

            if (!isEnd && !selfClosing && name is "script" or "style")
            {
                // 脚本和样式的内容是原始文本，直接跳到对应的结束标签之后
                var close = _html.IndexOf("</" + name, tagEnd + 1, _end - tagEnd - 1, StringComparison.OrdinalIgnoreCase);
                if (close < 0) return _end;
                var closeEnd = _html.IndexOf('>', close, _end - close);
                return closeEnd < 0 ? _end : closeEnd + 1;
            }

            return tagEnd + 1;
        }

        private static bool _IsNameChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or ':' or '_';

        /// <summary>
        /// 找到标签结尾的 '&gt;'，属性值里的引号内的 '&gt;' 不算。找不到时返回 -1。
        /// </summary>
        private int _FindTagEnd(int from)
        {
            var quote = '\0';
            var previous = '\0';
            for (var i = from; i < _end; i++)
            {
                var c = _html[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                        previous = c;
                    }

                    continue;
                }

                if (c == '>') return i;
                // 只有紧跟在 '=' 后面的引号才表示属性值的开始，避免未配对的引号吞掉后面的内容
                if ((c is '"' or '\'') && previous == '=') quote = c;
                if (!char.IsWhiteSpace(c)) previous = c;
            }

            return -1;
        }

        private void _HandleTag(string name, bool isEnd, int attrStart, int attrEnd, bool selfClosing)
        {
            if (_skipTag is not null)
            {
                if (name == _skipTag)
                {
                    if (isEnd)
                    {
                        if (--_skipDepth <= 0) _skipTag = null;
                    }
                    else if (!selfClosing)
                    {
                        _skipDepth++;
                    }
                }

                return;
            }

            if (!isEnd && !selfClosing && _SkippedContainers.Contains(name))
            {
                _Flush();
                _skipTag = name;
                _skipDepth = 1;
                return;
            }

            switch (name)
            {
                case "br":
                    if (!isEnd) _AppendBreak();
                    return;
                case "img":
                    if (!isEnd) _AddImage(attrStart, attrEnd);
                    return;
                case "a":
                    _CloseLink();
                    if (!isEnd) _OpenLink(attrStart, attrEnd);
                    return;
                case "ul":
                case "ol":
                    _Flush();
                    if (!isEnd)
                    {
                        var ordered = name == "ol";
                        _lists.Add(new ListState(ordered, ordered ? _ReadListStart(attrStart, attrEnd) - 1 : 0));
                    }
                    else if (_lists.Count > 0)
                    {
                        _lists.RemoveAt(_lists.Count - 1);
                    }

                    return;
                case "li":
                    if (isEnd) _CloseBlock();
                    else _OpenListItem();
                    return;
                case "figcaption":
                    if (isEnd) _CloseBlock();
                    else _OpenBlock(NewsArticleBlockKind.Caption, 0, 0);
                    return;
                case "td":
                case "th":
                    if (!isEnd) _AppendCellSeparator();
                    return;
            }

            if (name.Length == 2 && name[0] == 'h' && name[1] is >= '1' and <= '6')
            {
                if (isEnd) _CloseBlock();
                else _OpenBlock(NewsArticleBlockKind.Heading, name[1] - '0', 0);
                return;
            }

            if (_BlockTags.Contains(name)) _Flush();
        }

        /// <summary>
        /// 读取标签的属性。属性名不区分大小写，值已解码 HTML 实体，重复的属性取第一个。
        /// </summary>
        private Dictionary<string, string> _ReadAttributes(int from, int to)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var i = from;
            while (i < to)
            {
                while (i < to && (char.IsWhiteSpace(_html[i]) || _html[i] == '/')) i++;
                if (i >= to) break;

                var nameStart = i;
                while (i < to && !char.IsWhiteSpace(_html[i]) && _html[i] != '=' && _html[i] != '/') i++;
                var attributeName = _html.Substring(nameStart, i - nameStart);
                while (i < to && char.IsWhiteSpace(_html[i])) i++;

                var value = "";
                if (i < to && _html[i] == '=')
                {
                    i++;
                    while (i < to && char.IsWhiteSpace(_html[i])) i++;
                    if (i < to && (_html[i] is '"' or '\''))
                    {
                        var quote = _html[i];
                        i++;
                        var valueStart = i;
                        while (i < to && _html[i] != quote) i++;
                        value = _html.Substring(valueStart, i - valueStart);
                        if (i < to) i++; // 跳过结束引号
                    }
                    else
                    {
                        var valueStart = i;
                        while (i < to && !char.IsWhiteSpace(_html[i])) i++;
                        value = _html.Substring(valueStart, i - valueStart);
                    }
                }

                if (attributeName.Length > 0 && !attributes.ContainsKey(attributeName))
                    attributes[attributeName] = WebUtility.HtmlDecode(value);
            }

            return attributes;
        }

        #endregion

        #region 文本累积

        private void _AppendText(int from, int to)
        {
            if (_skipTag is not null || to <= from) return;
            var raw = _html.Substring(from, to - from);
            var text = raw.Contains('&') ? WebUtility.HtmlDecode(raw) : raw;
            foreach (var c in text) _AppendChar(c);
        }

        /// <summary>
        /// 追加一个字符。空白（含不换行空格）合并为一个空格，块开头的空白被丢弃，零宽字符被忽略。
        /// </summary>
        private void _AppendChar(char c)
        {
            if (_skipTag is not null) return;
            if (char.IsWhiteSpace(c))
            {
                if (_text.Length == 0) return;
                var last = _text[_text.Length - 1];
                if (last is ' ' or '\n') return;
                _text.Append(' ');
                return;
            }

            if (_IsIgnorable(c) || char.IsControl(c)) return;
            _text.Append(c);
        }

        private static bool _IsIgnorable(char c) => (int)c is 0x200B or 0x200C or 0x200D or 0x2060 or 0xFEFF or 0x00AD;

        private void _AppendBreak()
        {
            while (_text.Length > 0 && _text[_text.Length - 1] == ' ') _text.Length--;
            if (_text.Length == 0 || _text[_text.Length - 1] == '\n') return;
            _text.Append('\n');
        }

        private void _AppendCellSeparator()
        {
            if (_text.Length == 0) return;
            _AppendChar(' ');
            _text.Append('|');
            _AppendChar(' ');
        }

        private static string _NormalizeInline(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var builder = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (builder.Length > 0 && builder[builder.Length - 1] != ' ') builder.Append(' ');
                }
                else if (!_IsIgnorable(c) && !char.IsControl(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().TrimEnd();
        }

        #endregion

        #region 块

        private void _OpenBlock(NewsArticleBlockKind kind, int level, int index)
        {
            _Flush();
            _kind = kind;
            _level = level;
            _index = index;
        }

        private void _CloseBlock()
        {
            _Flush();
            _ResetKind();
        }

        private void _ResetKind()
        {
            _kind = NewsArticleBlockKind.Paragraph;
            _level = 0;
            _index = 0;
        }

        private void _OpenListItem()
        {
            if (_lists.Count == 0)
            {
                // 缺少外层列表的孤立列表项，按一层无序列表处理
                _OpenBlock(NewsArticleBlockKind.ListItem, 1, 0);
                return;
            }

            var list = _lists[_lists.Count - 1];
            list.Counter++;
            _OpenBlock(NewsArticleBlockKind.ListItem, _lists.Count, list.Ordered ? list.Counter : 0);
        }

        private int _ReadListStart(int attrStart, int attrEnd)
        {
            var attributes = _ReadAttributes(attrStart, attrEnd);
            if (attributes.TryGetValue("start", out var value) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start))
            {
                return Math.Max(start, 1);
            }

            return 1;
        }

        /// <summary>
        /// 把已累积的文本作为一个块输出。去掉首尾空白后为空的块（例如只含不换行空格的空段落）会被丢弃。
        /// </summary>
        private void _Flush()
        {
            _CloseLinkRange();

            var length = _text.Length;
            while (length > 0 && _text[length - 1] is ' ' or '\n') length--;
            if (length > 0)
            {
                var links = new List<NewsArticleLink>();
                foreach (var link in _links)
                {
                    var linkEnd = Math.Min(link.Start + link.Length, length);
                    if (linkEnd > link.Start) links.Add(link with { Length = linkEnd - link.Start });
                }

                var block = new NewsArticleBlock
                {
                    Kind = _kind,
                    Text = _text.ToString(0, length),
                    Level = _level,
                    Index = _index
                };
                _blocks.Add(links.Count == 0 ? block : block with { Links = links });
                _ResetKind();
            }

            _text.Clear();
            _links.Clear();
            if (_linkUrl is not null) _linkStart = 0; // 链接跨越了块的边界，在下一个块里继续
        }

        #endregion

        #region 链接与图片

        private void _OpenLink(int attrStart, int attrEnd)
        {
            var attributes = _ReadAttributes(attrStart, attrEnd);
            _linkUrl = attributes.TryGetValue("href", out var href) ? _ResolveUrl(href) : null;
            _linkStart = _text.Length;
        }

        private void _CloseLink()
        {
            _CloseLinkRange();
            _linkUrl = null;
        }

        /// <summary>
        /// 把当前打开的链接到目前为止的文字记为一段链接，首尾空白不计入。
        /// </summary>
        private void _CloseLinkRange()
        {
            if (_linkUrl is null) return;
            var start = _linkStart;
            var end = _text.Length;
            while (start < end && _text[start] is ' ' or '\n') start++;
            while (end > start && _text[end - 1] is ' ' or '\n') end--;
            if (end > start) _links.Add(new NewsArticleLink(start, end - start, _linkUrl));
            _linkStart = _text.Length;
        }

        private void _AddImage(int attrStart, int attrEnd)
        {
            var attributes = _ReadAttributes(attrStart, attrEnd);
            if (_IsTrackingPixel(attributes)) return;

            string? url = null;
            foreach (var key in _ImageSourceKeys)
            {
                if (!attributes.TryGetValue(key, out var value)) continue;
                url = _ResolveUrl(value);
                if (url is not null) break;
            }

            if (url is null && attributes.TryGetValue("srcset", out var srcSet))
                url = _ResolveUrl(_FirstSrcSetUrl(srcSet));
            if (url is null) return;

            _Flush();
            if (_blocks.Count > 0)
            {
                var last = _blocks[_blocks.Count - 1];
                if (last.Kind == NewsArticleBlockKind.Image && last.Url == url) return; // 相邻的重复图片
            }

            attributes.TryGetValue("alt", out var alt);
            _blocks.Add(new NewsArticleBlock
            {
                Kind = NewsArticleBlockKind.Image,
                Url = url,
                Text = _NormalizeInline(alt)
            });
        }

        private static bool _IsTrackingPixel(Dictionary<string, string> attributes)
        {
            if (attributes.TryGetValue("width", out var width) && (width.Trim() is "0" or "1")) return true;
            return attributes.TryGetValue("height", out var height) && (height.Trim() is "0" or "1");
        }

        private static string _FirstSrcSetUrl(string srcSet)
        {
            var first = srcSet.Split(',')[0].Trim();
            var space = first.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return space < 0 ? first : first.Substring(0, space);
        }

        /// <summary>
        /// 把相对地址补全为绝对地址。只接受 http 与 https，页内锚点、mailto、javascript 等一律返回 null。
        /// </summary>
        private string? _ResolveUrl(string? raw)
        {
            if (raw is null) return null;
            raw = raw.Trim();
            if (raw.Length == 0 || raw[0] == '#') return null;
            if (!Uri.TryCreate(_baseUri, raw, out var uri)) return null;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
        }

        #endregion
    }
}
