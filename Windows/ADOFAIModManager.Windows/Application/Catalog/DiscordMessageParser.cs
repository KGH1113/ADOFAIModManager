using System.Text.RegularExpressions;

namespace ADOFAIModManager.Windows.Application.Catalog;

internal abstract record DiscordMessageBlock;
internal sealed record DiscordParagraph(IReadOnlyList<DiscordInline> Content) : DiscordMessageBlock;
internal sealed record DiscordSubtext(IReadOnlyList<DiscordInline> Content) : DiscordMessageBlock;
internal sealed record DiscordHeading(int Level, IReadOnlyList<DiscordInline> Content) : DiscordMessageBlock;
internal sealed record DiscordBulletList(IReadOnlyList<IReadOnlyList<DiscordInline>> Items) : DiscordMessageBlock;
internal sealed record DiscordOrderedList(int Start, IReadOnlyList<IReadOnlyList<DiscordInline>> Items) : DiscordMessageBlock;
internal sealed record DiscordQuote(IReadOnlyList<DiscordMessageBlock> Blocks) : DiscordMessageBlock;
internal sealed record DiscordCodeBlock(string? Language, string Code) : DiscordMessageBlock;
internal sealed record DiscordTable(IReadOnlyList<IReadOnlyList<IReadOnlyList<DiscordInline>>> Rows) : DiscordMessageBlock;
internal sealed record DiscordDivider : DiscordMessageBlock;

internal sealed record DiscordInline(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Underline = false,
    bool Strikethrough = false,
    bool Spoiler = false,
    bool Code = false,
    Uri? Link = null);

internal static partial class DiscordMessageParser
{
    [Flags]
    private enum InlineStyle { None = 0, Bold = 1, Italic = 2, Underline = 4, Strikethrough = 8, Spoiler = 16, Code = 32 }

    public static IReadOnlyList<DiscordMessageBlock> Parse(string message)
    {
        var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n');
        var blocks = new List<DiscordMessageBlock>();
        for (var index = 0; index < lines.Length;)
        {
            if (string.IsNullOrWhiteSpace(lines[index])) { index++; continue; }
            if (lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                var fence = lines[index].Trim();
                var language = fence.Length > 3 ? fence[3..].Trim() : null;
                index++;
                var code = new List<string>();
                while (index < lines.Length && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
                    code.Add(lines[index++]);
                if (index < lines.Length) index++;
                blocks.Add(new DiscordCodeBlock(string.IsNullOrWhiteSpace(language) ? null : language, string.Join("\n", code)));
                continue;
            }

            if (IsTableHeader(lines, index))
            {
                var rows = new List<IReadOnlyList<IReadOnlyList<DiscordInline>>> { SplitTableRow(lines[index]) };
                index += 2;
                while (index < lines.Length && lines[index].Contains('|') && !string.IsNullOrWhiteSpace(lines[index]))
                    rows.Add(SplitTableRow(lines[index++]));
                blocks.Add(new DiscordTable(rows));
                continue;
            }

            var heading = HeadingPattern().Match(lines[index]);
            if (heading.Success)
            {
                blocks.Add(new DiscordHeading(heading.Groups[1].Length, ParseInline(heading.Groups[2].Value)));
                index++;
                continue;
            }

            if (lines[index].TrimStart().StartsWith("-# ", StringComparison.Ordinal))
            {
                blocks.Add(new DiscordSubtext(ParseInline(lines[index].TrimStart()[3..])));
                index++;
                continue;
            }

            if (BulletPattern().IsMatch(lines[index]))
            {
                var items = new List<IReadOnlyList<DiscordInline>>();
                while (index < lines.Length)
                {
                    var match = BulletPattern().Match(lines[index]);
                    if (!match.Success) break;
                    items.Add(ParseInline(match.Groups[1].Value));
                    index++;
                }
                blocks.Add(new DiscordBulletList(items));
                continue;
            }

            var ordered = OrderedPattern().Match(lines[index]);
            if (ordered.Success)
            {
                var start = int.Parse(ordered.Groups[1].Value);
                var items = new List<IReadOnlyList<DiscordInline>>();
                while (index < lines.Length)
                {
                    var match = OrderedPattern().Match(lines[index]);
                    if (!match.Success) break;
                    items.Add(ParseInline(match.Groups[2].Value));
                    index++;
                }
                blocks.Add(new DiscordOrderedList(start, items));
                continue;
            }

            if (IsDivider(lines[index]))
            {
                blocks.Add(new DiscordDivider());
                index++;
                continue;
            }

            if (lines[index].TrimStart().StartsWith('>'))
            {
                var quote = new List<string>();
                while (index < lines.Length && lines[index].TrimStart().StartsWith('>'))
                {
                    var line = lines[index++].TrimStart()[1..];
                    quote.Add(line.StartsWith(' ') ? line[1..] : line);
                }
                blocks.Add(new DiscordQuote(Parse(string.Join("\n", quote))));
                continue;
            }

            var paragraph = new List<string> { lines[index++] };
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index])
                   && !HeadingPattern().IsMatch(lines[index])
                   && !BulletPattern().IsMatch(lines[index])
                   && !OrderedPattern().IsMatch(lines[index])
                   && !lines[index].TrimStart().StartsWith('>')
                   && !lines[index].TrimStart().StartsWith("-# ", StringComparison.Ordinal)
                   && !IsDivider(lines[index])
                   && !IsTableHeader(lines, index)
                   && !lines[index].TrimStart().StartsWith("```", StringComparison.Ordinal))
                paragraph.Add(lines[index++]);
            blocks.Add(new DiscordParagraph(ParseInline(string.Join("\n", paragraph))));
        }
        return blocks;
    }

    internal static IReadOnlyList<DiscordInline> ParseInline(string text)
    {
        var result = new List<DiscordInline>();
        var buffer = new System.Text.StringBuilder();
        var styles = InlineStyle.None;
        var index = 0;

        void Flush(InlineStyle extra = InlineStyle.None, string? content = null, Uri? link = null)
        {
            var value = content ?? buffer.ToString();
            if (content is null) buffer.Clear();
            if (value.Length == 0) return;
            var active = styles | extra;
            result.Add(new DiscordInline(value,
                Bold: active.HasFlag(InlineStyle.Bold),
                Italic: active.HasFlag(InlineStyle.Italic),
                Underline: active.HasFlag(InlineStyle.Underline),
                Strikethrough: active.HasFlag(InlineStyle.Strikethrough),
                Spoiler: active.HasFlag(InlineStyle.Spoiler),
                Code: active.HasFlag(InlineStyle.Code),
                Link: link));
        }

        while (index < text.Length)
        {
            if (text[index] == '\\' && index + 1 < text.Length)
            {
                buffer.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (text[index] == '[')
            {
                var separator = text.IndexOf("](", index, StringComparison.Ordinal);
                var closing = separator >= 0 ? text.IndexOf(')', separator + 2) : -1;
                if (separator > index && closing > separator && SafeLink(text[(separator + 2)..closing]) is { } link)
                {
                    Flush();
                    foreach (var item in ParseInline(text[(index + 1)..separator])) result.Add(item with { Link = link });
                    index = closing + 1;
                    continue;
                }
            }

            if (text[index] == '<' && text.IndexOf('>', index + 1) is var angleEnd && angleEnd > index)
            {
                Flush();
                var token = ParseAngleToken(text[(index + 1)..angleEnd]);
                result.Add(token with
                {
                    Bold = token.Bold || styles.HasFlag(InlineStyle.Bold),
                    Italic = token.Italic || styles.HasFlag(InlineStyle.Italic),
                    Underline = token.Underline || styles.HasFlag(InlineStyle.Underline),
                    Strikethrough = token.Strikethrough || styles.HasFlag(InlineStyle.Strikethrough),
                    Spoiler = token.Spoiler || styles.HasFlag(InlineStyle.Spoiler)
                });
                index = angleEnd + 1;
                continue;
            }

            if (text.AsSpan(index).StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || text.AsSpan(index).StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                var end = index;
                while (end < text.Length && !char.IsWhiteSpace(text[end]) && !"<>\"".Contains(text[end])) end++;
                var raw = text[index..end];
                var address = raw.TrimEnd('.', ',', ';', '!', '?');
                Flush(content: address, link: SafeLink(address));
                if (address.Length < raw.Length) Flush(content: raw[address.Length..]);
                index = end;
                continue;
            }

            if (text[index] == '`' && text.IndexOf('`', index + 1) is var codeEnd && codeEnd > index)
            {
                Flush();
                Flush(InlineStyle.Code, text[(index + 1)..codeEnd]);
                index = codeEnd + 1;
                continue;
            }

            var markers = new (string Marker, InlineStyle Style)[]
            {
                ("**", InlineStyle.Bold), ("__", InlineStyle.Underline), ("~~", InlineStyle.Strikethrough),
                ("||", InlineStyle.Spoiler), ("*", InlineStyle.Italic), ("_", InlineStyle.Italic)
            };
            var consumed = false;
            foreach (var (marker, style) in markers)
            {
                if (!text.AsSpan(index).StartsWith(marker, StringComparison.Ordinal)) continue;
                if ((marker == "*" && text.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
                    || (marker == "_" && text.AsSpan(index).StartsWith("__", StringComparison.Ordinal))) continue;
                var closing = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
                if (!styles.HasFlag(style) && closing < 0) continue;
                Flush();
                styles = styles.HasFlag(style) ? styles & ~style : styles | style;
                index += marker.Length;
                consumed = true;
                break;
            }
            if (consumed) continue;

            buffer.Append(text[index]);
            index++;
        }
        Flush();
        return result;
    }

    private static DiscordInline ParseAngleToken(string token)
    {
        if (SafeLink(token) is { } link) return new DiscordInline(token, Link: link);
        if (token.StartsWith("@&", StringComparison.Ordinal)) return new DiscordInline("@role");
        if (token.StartsWith('@')) return new DiscordInline("@user");
        if (token.StartsWith('#')) return new DiscordInline("#channel");
        if (token.StartsWith("a:", StringComparison.Ordinal) || token.StartsWith(':'))
        {
            var parts = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return new DiscordInline($":{parts[^2]}:");
        }
        if (token.StartsWith("t:", StringComparison.Ordinal))
        {
            var parts = token.Split(':');
            if (parts.Length >= 2 && long.TryParse(parts[1], out var timestamp))
                return new DiscordInline(DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("g"));
        }
        return new DiscordInline($"<{token}>");
    }

    private static Uri? SafeLink(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;

    private static bool IsDivider(string line)
    {
        var compact = new string(line.Where(character => !char.IsWhiteSpace(character)).ToArray());
        return compact.Length >= 3 && (compact.All(character => character == '-')
            || compact.All(character => character == '_')
            || (compact.Length == 3 && compact.All(character => character == '*')));
    }

    private static bool IsTableHeader(string[] lines, int index)
    {
        if (index + 1 >= lines.Length || !lines[index].Contains('|')) return false;
        var markers = SplitTableCells(lines[index + 1]);
        return markers.Count > 0 && markers.All(marker =>
        {
            var core = marker.Trim().Trim(':');
            return core.Length >= 3 && core.All(character => character == '-');
        });
    }

    private static IReadOnlyList<IReadOnlyList<DiscordInline>> SplitTableRow(string line) =>
        SplitTableCells(line).Select(ParseInline).ToArray();

    private static List<string> SplitTableCells(string line)
    {
        var cells = line.Split('|').Select(cell => cell.Trim()).ToList();
        if (cells.FirstOrDefault()?.Length == 0) cells.RemoveAt(0);
        if (cells.LastOrDefault()?.Length == 0) cells.RemoveAt(cells.Count - 1);
        return cells;
    }

    [GeneratedRegex("^(#{1,3})\\s+(.+)$")]
    private static partial Regex HeadingPattern();
    [GeneratedRegex("^\\s*[-*+]\\s+(.+)$")]
    private static partial Regex BulletPattern();
    [GeneratedRegex("^\\s*(\\d+)[.)]\\s+(.+)$")]
    private static partial Regex OrderedPattern();
}
