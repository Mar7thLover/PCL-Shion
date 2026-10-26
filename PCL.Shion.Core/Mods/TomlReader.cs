using System.Globalization;
using System.Text;

namespace PCL.Shion.Core.Mods;

/// <summary>
/// TOML 的最小读取器，只覆盖 mods.toml 用到的子集：注释、表与表数组、点分键、
/// 各类字符串（含多行）、整数、浮点数、布尔值、数组与内联表；不支持日期时间。
/// </summary>
internal sealed class TomlReader
{
    private readonly string _text;
    private int _position;
    private int _valueDepth;
    private const int MaxValueDepth = 64;

    private TomlReader(string text)
    {
        _text = text;
    }

    /// <summary>
    /// 解析 TOML 文本。
    /// </summary>
    /// <exception cref="FormatException">文本不符合支持的 TOML 语法。</exception>
    public static TomlTable Parse(string text) => new TomlReader(text).ParseDocument();

    private bool AtEnd => _position >= _text.Length;

    private char Peek => AtEnd ? '\0' : _text[_position];

    private TomlTable ParseDocument()
    {
        var root = new TomlTable();
        var current = root;
        while (true)
        {
            SkipBlank();
            if (AtEnd)
                return root;

            if (Peek == '[')
            {
                var isArray = IsAt('[', 2);
                _position += isArray ? 2 : 1;
                var path = ReadKeyPath();
                Expect(']');
                if (isArray)
                    Expect(']');
                current = isArray ? AddArrayTable(root, path) : GetOrAddTable(root, path);
            }
            else
            {
                var path = ReadKeyPath();
                Expect('=');
                SkipSpaces();
                Assign(current, path, ReadValue());
            }
            EndLine();
        }
    }

    private TomlTable GetOrAddTable(TomlTable root, List<string> path)
    {
        var table = root;
        foreach (var key in path)
            table = Descend(table, key);
        return table;
    }

    private TomlTable AddArrayTable(TomlTable root, List<string> path)
    {
        var parent = root;
        for (var i = 0; i < path.Count - 1; i++)
            parent = Descend(parent, path[i]);

        var name = path[^1];
        var created = new TomlTable();
        if (!parent.TryGetValue(name, out var existing))
            parent[name] = new List<object> { created };
        else if (existing is List<object> tables && tables.All(item => item is TomlTable))
            tables.Add(created);
        else
            throw Error($"'{name}' is already defined as a value");
        return created;
    }

    private void Assign(TomlTable table, List<string> path, object value)
    {
        var target = table;
        for (var i = 0; i < path.Count - 1; i++)
            target = Descend(target, path[i]);

        if (!target.TryAdd(path[^1], value))
            throw Error($"Duplicate key '{path[^1]}'");
    }

    // 取子表，不存在时创建；键指向表数组时进入其最后一个元素
    private TomlTable Descend(TomlTable table, string key)
    {
        if (!table.TryGetValue(key, out var value))
        {
            var created = new TomlTable();
            table[key] = created;
            return created;
        }

        return value switch
        {
            TomlTable existing => existing,
            List<object> { Count: > 0 } list when list[^1] is TomlTable last => last,
            _ => throw Error($"'{key}' is already defined as a value")
        };
    }

    private List<string> ReadKeyPath()
    {
        var path = new List<string>();
        while (true)
        {
            SkipSpaces();
            path.Add(ReadKey());
            SkipSpaces();
            if (Peek != '.')
                return path;
            _position++;
        }
    }

    private string ReadKey()
    {
        if (Peek is '"' or '\'')
            return ReadString();

        var start = _position;
        while (char.IsAsciiLetterOrDigit(Peek) || Peek is '_' or '-')
            _position++;
        return _position > start ? _text[start.._position] : throw Error("Expected a key");
    }

    private object ReadValue()
    {
        if (_valueDepth >= MaxValueDepth)
            throw Error("Nested TOML value exceeded the maximum depth");
        _valueDepth++;
        try
        {
            switch (Peek)
            {
                case '"':
                case '\'':
                    return ReadString();
                case '[':
                    return ReadArray();
                case '{':
                    return ReadInlineTable();
                default:
                    return ReadScalar();
            }
        }
        finally
        {
            _valueDepth--;
        }
    }

    private List<object> ReadArray()
    {
        _position++;
        var items = new List<object>();
        while (true)
        {
            SkipBlank();
            if (Peek == ']')
            {
                _position++;
                return items;
            }

            items.Add(ReadValue());
            SkipBlank();
            if (Peek == ',')
                _position++;
            else if (Peek != ']')
                throw Error("Expected ',' or ']'");
        }
    }

    private TomlTable ReadInlineTable()
    {
        _position++;
        var table = new TomlTable();
        while (true)
        {
            SkipBlank();
            if (Peek == '}')
            {
                _position++;
                return table;
            }

            var path = ReadKeyPath();
            Expect('=');
            SkipSpaces();
            Assign(table, path, ReadValue());
            SkipBlank();
            if (Peek == ',')
                _position++;
            else if (Peek != '}')
                throw Error("Expected ',' or '}'");
        }
    }

    // 布尔值与数字；其他不带引号的内容原样当作字符串
    private object ReadScalar()
    {
        var start = _position;
        while (!AtEnd && Peek is not (',' or ']' or '}' or '#' or ' ' or '\t' or '\r' or '\n'))
            _position++;

        var token = _text[start.._position];
        if (token.Length == 0)
            throw Error("Expected a value");
        if (token == "true")
            return true;
        if (token == "false")
            return false;

        var number = token.Replace("_", string.Empty);
        if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            return integer;
        if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
            return real;
        return token;
    }

    // 位置在起始引号上；基本字符串处理转义，字面字符串原样保留，三引号为多行
    private string ReadString()
    {
        var quote = Peek;
        var literal = quote == '\'';
        var multiline = IsAt(quote, 3);
        _position += multiline ? 3 : 1;
        if (multiline)
            SkipNewline();

        var builder = new StringBuilder();
        while (true)
        {
            if (AtEnd)
                throw Error("Unterminated string");

            var c = _text[_position];
            if (c == quote)
            {
                if (!multiline)
                {
                    _position++;
                    return builder.ToString();
                }

                // 结尾的三个引号之前最多还可以有两个引号属于内容
                var run = CountRun(quote);
                if (run >= 3)
                {
                    builder.Append(quote, run - 3);
                    _position += run;
                    return builder.ToString();
                }
                builder.Append(quote, run);
                _position += run;
            }
            else if (c == '\\' && !literal)
            {
                ReadEscape(builder, multiline);
            }
            else if (!multiline && (c is '\r' or '\n'))
            {
                throw Error("Newline in single-line string");
            }
            else
            {
                builder.Append(c);
                _position++;
            }
        }
    }

    private void ReadEscape(StringBuilder builder, bool multiline)
    {
        _position++;
        if (AtEnd)
            throw Error("Unterminated escape sequence");

        var c = _text[_position++];
        switch (c)
        {
            case 'b':
                builder.Append('\b');
                break;
            case 't':
                builder.Append('\t');
                break;
            case 'n':
                builder.Append('\n');
                break;
            case 'f':
                builder.Append('\f');
                break;
            case 'r':
                builder.Append('\r');
                break;
            case '"':
            case '\\':
                builder.Append(c);
                break;
            case 'u':
                AppendCodePoint(builder, ReadHex(4));
                break;
            case 'U':
                AppendCodePoint(builder, ReadHex(8));
                break;
            case ' ' or '\t' or '\r' or '\n' when multiline:
                // 多行字符串中行尾的反斜杠会吞掉其后的所有空白与换行
                _position--;
                while (char.IsWhiteSpace(Peek))
                    _position++;
                break;
            default:
                throw Error($"Invalid escape sequence '\\{c}'");
        }
    }

    private int ReadHex(int count)
    {
        if (_position + count <= _text.Length
            && int.TryParse(_text.AsSpan(_position, count), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            _position += count;
            return value;
        }
        throw Error("Invalid unicode escape");
    }

    private void AppendCodePoint(StringBuilder builder, int codePoint)
    {
        if (!Rune.IsValid(codePoint))
            throw Error("Invalid unicode escape");
        builder.Append(new Rune(codePoint).ToString());
    }

    private bool IsAt(char c, int count)
    {
        if (_position + count > _text.Length)
            return false;
        for (var i = 0; i < count; i++)
        {
            if (_text[_position + i] != c)
                return false;
        }
        return true;
    }

    private int CountRun(char c)
    {
        var end = _position;
        while (end < _text.Length && _text[end] == c)
            end++;
        return end - _position;
    }

    private void Expect(char expected)
    {
        SkipSpaces();
        if (Peek != expected)
            throw Error($"Expected '{expected}'");
        _position++;
    }

    // 一个键值对或表头之后只能是空白、注释和换行
    private void EndLine()
    {
        SkipSpaces();
        if (Peek == '#')
            SkipLine();
        if (!AtEnd && Peek is not ('\r' or '\n'))
            throw Error("Unexpected characters after value");
    }

    private void SkipSpaces()
    {
        while (Peek is ' ' or '\t')
            _position++;
    }

    private void SkipLine()
    {
        while (!AtEnd && _text[_position] != '\n')
            _position++;
    }

    private void SkipBlank()
    {
        while (!AtEnd)
        {
            var c = _text[_position];
            if (c is ' ' or '\t' or '\r' or '\n')
                _position++;
            else if (c == '#')
                SkipLine();
            else
                return;
        }
    }

    // 多行字符串开头定界符后紧跟的换行不属于内容
    private void SkipNewline()
    {
        if (Peek == '\r')
            _position++;
        if (Peek == '\n')
            _position++;
    }

    private FormatException Error(string message)
    {
        var line = 1 + _text.AsSpan(0, Math.Min(_position, _text.Length)).Count('\n');
        return new FormatException($"{message} (line {line})");
    }
}
