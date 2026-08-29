import SwiftUI

indirect enum DiscordMessageBlock: Equatable, Sendable {
    case paragraph(String)
    case subtext(String)
    case heading(level: Int, text: String)
    case quote([DiscordMessageBlock])
    case unorderedList([String])
    case orderedList(start: Int, items: [String])
    case codeBlock(language: String?, code: String)
    case table([[String]])
    case divider
}

enum DiscordMessageParser {
    static func parse(_ message: String) -> [DiscordMessageBlock] {
        let normalized = message.replacingOccurrences(of: "\r\n", with: "\n")
            .replacingOccurrences(of: "\r", with: "\n")
        return parseLines(normalized.components(separatedBy: "\n"))
    }

    private static func parseLines(_ lines: [String]) -> [DiscordMessageBlock] {
        var blocks: [DiscordMessageBlock] = []
        var index = 0

        while index < lines.count {
            let line = lines[index]
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty {
                index += 1
                continue
            }

            if trimmed.hasPrefix("```") {
                let language = String(trimmed.dropFirst(3)).trimmingCharacters(in: .whitespaces)
                index += 1
                var codeLines: [String] = []
                while index < lines.count && !lines[index].trimmingCharacters(in: .whitespaces).hasPrefix("```") {
                    codeLines.append(lines[index])
                    index += 1
                }
                if index < lines.count { index += 1 }
                blocks.append(.codeBlock(language: language.isEmpty ? nil : language, code: codeLines.joined(separator: "\n")))
                continue
            }

            if isTableHeader(at: index, in: lines) {
                var rows = [splitTableRow(lines[index])]
                index += 2
                while index < lines.count, lines[index].contains("|"), !lines[index].trimmingCharacters(in: .whitespaces).isEmpty {
                    rows.append(splitTableRow(lines[index]))
                    index += 1
                }
                blocks.append(.table(rows))
                continue
            }

            if let heading = heading(from: trimmed) {
                blocks.append(.heading(level: heading.level, text: heading.text))
                index += 1
                continue
            }

            if trimmed.hasPrefix("-# ") {
                blocks.append(.subtext(String(trimmed.dropFirst(3))))
                index += 1
                continue
            }

            if trimmed.hasPrefix(">") {
                var quotedLines: [String] = []
                while index < lines.count {
                    let candidate = lines[index].trimmingCharacters(in: .whitespaces)
                    guard candidate.hasPrefix(">") else { break }
                    var content = String(candidate.dropFirst())
                    if content.hasPrefix(" ") { content.removeFirst() }
                    quotedLines.append(content)
                    index += 1
                }
                blocks.append(.quote(parseLines(quotedLines)))
                continue
            }

            if let item = unorderedItem(from: trimmed) {
                var items = [item]
                index += 1
                while index < lines.count, let next = unorderedItem(from: lines[index].trimmingCharacters(in: .whitespaces)) {
                    items.append(next)
                    index += 1
                }
                blocks.append(.unorderedList(items))
                continue
            }

            if let first = orderedItem(from: trimmed) {
                var items = [first.text]
                index += 1
                while index < lines.count, let next = orderedItem(from: lines[index].trimmingCharacters(in: .whitespaces)) {
                    items.append(next.text)
                    index += 1
                }
                blocks.append(.orderedList(start: first.number, items: items))
                continue
            }

            if isDivider(trimmed) {
                blocks.append(.divider)
                index += 1
                continue
            }

            var paragraph = [line]
            index += 1
            while index < lines.count {
                let candidate = lines[index]
                let candidateTrimmed = candidate.trimmingCharacters(in: .whitespaces)
                guard !candidateTrimmed.isEmpty,
                      !startsBlock(candidateTrimmed),
                      !isTableHeader(at: index, in: lines) else { break }
                paragraph.append(candidate)
                index += 1
            }
            blocks.append(.paragraph(paragraph.joined(separator: "\n")))
        }
        return blocks
    }

    private static func startsBlock(_ line: String) -> Bool {
        line.hasPrefix("```") || line.hasPrefix(">") || line.hasPrefix("-# ") || heading(from: line) != nil
            || unorderedItem(from: line) != nil || orderedItem(from: line) != nil || isDivider(line)
    }

    private static func heading(from line: String) -> (level: Int, text: String)? {
        let hashes = line.prefix { $0 == "#" }.count
        guard (1...3).contains(hashes) else { return nil }
        let remainder = line.dropFirst(hashes)
        guard remainder.first == " " else { return nil }
        return (hashes, String(remainder.dropFirst()))
    }

    private static func unorderedItem(from line: String) -> String? {
        for prefix in ["- ", "* ", "+ "] where line.hasPrefix(prefix) {
            return String(line.dropFirst(prefix.count))
        }
        return nil
    }

    private static func orderedItem(from line: String) -> (number: Int, text: String)? {
        let digits = line.prefix { $0.isNumber }
        guard let number = Int(digits), !digits.isEmpty else { return nil }
        let remainder = line.dropFirst(digits.count)
        guard remainder.hasPrefix(". ") || remainder.hasPrefix(") ") else { return nil }
        return (number, String(remainder.dropFirst(2)))
    }

    private static func isDivider(_ line: String) -> Bool {
        let compact = line.filter { !$0.isWhitespace }
        let characters = Set(compact)
        return compact.count >= 3 && (
            characters == Set<Character>(["-"])
                || characters == Set<Character>(["_"])
                || (compact.count == 3 && characters == Set<Character>(["*"]))
        )
    }

    private static func isTableHeader(at index: Int, in lines: [String]) -> Bool {
        guard index + 1 < lines.count, lines[index].contains("|") else { return false }
        let cells = splitTableRow(lines[index + 1])
        guard !cells.isEmpty else { return false }
        return cells.allSatisfy { cell in
            let marker = cell.trimmingCharacters(in: .whitespaces)
            let core = marker.trimmingCharacters(in: CharacterSet(charactersIn: ":"))
            return core.count >= 3 && core.allSatisfy { $0 == "-" }
        }
    }

    private static func splitTableRow(_ line: String) -> [String] {
        var cells = line.components(separatedBy: "|").map { $0.trimmingCharacters(in: .whitespaces) }
        if cells.first?.isEmpty == true { cells.removeFirst() }
        if cells.last?.isEmpty == true { cells.removeLast() }
        return cells
    }
}

struct DiscordMessageView: View {
    private let blocks: [DiscordMessageBlock]

    init(_ message: String) {
        blocks = DiscordMessageParser.parse(message)
    }

    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.rowSpacing) {
            ForEach(Array(blocks.enumerated()), id: \.offset) { _, block in
                DiscordBlockView(block: block)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct DiscordBlockView: View {
    let block: DiscordMessageBlock

    @ViewBuilder
    var body: some View {
        switch block {
        case let .paragraph(text):
            inlineText(text)
        case let .subtext(text):
            inlineText(text)
                .font(.caption)
                .foregroundStyle(.secondary)
        case let .heading(level, text):
            inlineText(text)
                .font(headingFont(for: level))
                .padding(.top, LayoutMetrics.compact)
        case let .quote(blocks):
            HStack(alignment: .top, spacing: LayoutMetrics.rowSpacing) {
                RoundedRectangle(cornerRadius: 2)
                    .fill(Color.secondary.opacity(0.5))
                    .frame(width: 3)
                VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                    ForEach(Array(blocks.enumerated()), id: \.offset) { _, block in
                        DiscordBlockView(block: block)
                    }
                }
            }
            .padding(.vertical, LayoutMetrics.compact / 2)
        case let .unorderedList(items):
            VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                    HStack(alignment: .firstTextBaseline, spacing: LayoutMetrics.compact) {
                        Text(verbatim: "•")
                        inlineText(item)
                    }
                }
            }
            .padding(.leading, LayoutMetrics.compact)
        case let .orderedList(start, items):
            VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                ForEach(Array(items.enumerated()), id: \.offset) { offset, item in
                    HStack(alignment: .firstTextBaseline, spacing: LayoutMetrics.compact) {
                        Text(verbatim: "\(start + offset).")
                            .foregroundStyle(.secondary)
                            .monospacedDigit()
                        inlineText(item)
                    }
                }
            }
            .padding(.leading, LayoutMetrics.compact)
        case let .codeBlock(language, code):
            VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                if let language {
                    Text(verbatim: language.uppercased())
                        .font(.caption2.weight(.semibold))
                        .foregroundStyle(.secondary)
                }
                ScrollView(.horizontal) {
                    Text(verbatim: code)
                        .font(.system(.body, design: .monospaced))
                        .textSelection(.enabled)
                        .padding(LayoutMetrics.rowSpacing)
                }
                .background(Color.secondary.opacity(0.08), in: RoundedRectangle(cornerRadius: LayoutMetrics.compact))
            }
        case let .table(rows):
            DiscordTableView(rows: rows)
        case .divider:
            Divider()
                .padding(.vertical, LayoutMetrics.compact / 2)
        }
    }

    private func inlineText(_ text: String) -> some View {
        Text(DiscordInlineParser.parse(text))
            .textSelection(.enabled)
            .fixedSize(horizontal: false, vertical: true)
    }

    private func headingFont(for level: Int) -> Font {
        switch level {
        case 1: .title2.bold()
        case 2: .title3.bold()
        default: .headline
        }
    }
}

private struct DiscordTableView: View {
    let rows: [[String]]

    var body: some View {
        ScrollView(.horizontal) {
            VStack(alignment: .leading, spacing: 0) {
                ForEach(Array(rows.enumerated()), id: \.offset) { rowIndex, row in
                    HStack(alignment: .top, spacing: 0) {
                        ForEach(Array(row.enumerated()), id: \.offset) { columnIndex, cell in
                            Text(DiscordInlineParser.parse(cell))
                                .font(rowIndex == 0 ? .headline : .body)
                                .textSelection(.enabled)
                                .frame(width: 190, alignment: .leading)
                                .padding(LayoutMetrics.compact)
                            if columnIndex < row.count - 1 { Divider() }
                        }
                    }
                    if rowIndex < rows.count - 1 { Divider() }
                }
            }
            .background(Color.secondary.opacity(0.05), in: RoundedRectangle(cornerRadius: LayoutMetrics.compact))
            .overlay {
                RoundedRectangle(cornerRadius: LayoutMetrics.compact)
                    .stroke(Color.secondary.opacity(0.2))
            }
        }
    }
}

enum DiscordInlineParser {
    private struct Style: OptionSet {
        let rawValue: Int
        static let bold = Style(rawValue: 1 << 0)
        static let italic = Style(rawValue: 1 << 1)
        static let underline = Style(rawValue: 1 << 2)
        static let strikethrough = Style(rawValue: 1 << 3)
        static let spoiler = Style(rawValue: 1 << 4)
        static let code = Style(rawValue: 1 << 5)
    }

    static func parse(_ source: String) -> AttributedString {
        var result = AttributedString()
        var index = source.startIndex
        var buffer = ""
        var styles: Style = []

        func styled(_ text: String, link: URL? = nil, extra: Style = []) -> AttributedString {
            var value = AttributedString(text)
            let active = styles.union(extra)
            if active.contains(.bold) { value.inlinePresentationIntent = .stronglyEmphasized }
            if active.contains(.italic) { value.inlinePresentationIntent = (value.inlinePresentationIntent ?? []).union(.emphasized) }
            if active.contains(.underline) { value.underlineStyle = .single }
            if active.contains(.strikethrough) { value.strikethroughStyle = .single }
            if active.contains(.spoiler) {
                value.foregroundColor = .primary
                value.backgroundColor = Color.secondary.opacity(0.18)
            }
            if active.contains(.code) {
                value.font = .system(.body, design: .monospaced)
                value.backgroundColor = Color.secondary.opacity(0.12)
            }
            if let link { value.link = link }
            return value
        }

        func flush() {
            guard !buffer.isEmpty else { return }
            result.append(styled(buffer))
            buffer = ""
        }

        while index < source.endIndex {
            if source[index] == "\\" {
                let next = source.index(after: index)
                if next < source.endIndex {
                    buffer.append(source[next])
                    index = source.index(after: next)
                    continue
                }
            }

            if source[index] == "[", let link = markdownLink(in: source, from: index) {
                flush()
                var label = parse(link.label)
                label.link = link.url
                result.append(label)
                index = link.endIndex
                continue
            }

            if source[index] == "<", let token = angleToken(in: source, from: index) {
                flush()
                result.append(styled(token.text, link: token.url))
                index = token.endIndex
                continue
            }

            let remainder = source[index...]
            if remainder.hasPrefix("https://") || remainder.hasPrefix("http://") {
                flush()
                let token = bareURL(in: source, from: index)
                result.append(styled(token.text, link: token.url))
                index = token.endIndex
                continue
            }

            if source[index] == "`", let closing = source[source.index(after: index)...].firstIndex(of: "`") {
                flush()
                let contentStart = source.index(after: index)
                result.append(styled(String(source[contentStart..<closing]), extra: .code))
                index = source.index(after: closing)
                continue
            }

            let markers: [(String, Style)] = [
                ("**", .bold), ("__", .underline), ("~~", .strikethrough), ("||", .spoiler), ("*", .italic), ("_", .italic)
            ]
            var consumedMarker = false
            for (marker, style) in markers where remainder.hasPrefix(marker) {
                if marker == "*" && remainder.hasPrefix("**") { continue }
                if marker == "_" && remainder.hasPrefix("__") { continue }
                let markerEnd = source.index(index, offsetBy: marker.count)
                let isClosing = styles.contains(style)
                let hasClosing = markerEnd < source.endIndex && source[markerEnd...].range(of: marker) != nil
                guard isClosing || hasClosing else { continue }
                flush()
                if isClosing { styles.remove(style) } else { styles.insert(style) }
                index = markerEnd
                consumedMarker = true
                break
            }
            if consumedMarker { continue }

            buffer.append(source[index])
            index = source.index(after: index)
        }
        flush()
        return result
    }

    private static func markdownLink(in source: String, from start: String.Index) -> (label: String, url: URL, endIndex: String.Index)? {
        guard let separator = source[start...].range(of: "]("),
              let closing = source[separator.upperBound...].firstIndex(of: ")") else { return nil }
        let labelStart = source.index(after: start)
        let address = String(source[separator.upperBound..<closing])
        guard labelStart <= separator.lowerBound, let url = safeURL(address) else { return nil }
        return (String(source[labelStart..<separator.lowerBound]), url, source.index(after: closing))
    }

    private static func angleToken(in source: String, from start: String.Index) -> (text: String, url: URL?, endIndex: String.Index)? {
        guard let closing = source[start...].firstIndex(of: ">") else { return nil }
        let contentStart = source.index(after: start)
        let content = String(source[contentStart..<closing])
        let end = source.index(after: closing)
        if let url = safeURL(content) { return (content, url, end) }
        if content.hasPrefix("@&") { return ("@role", nil, end) }
        if content.hasPrefix("@") { return ("@user", nil, end) }
        if content.hasPrefix("#") { return ("#channel", nil, end) }
        if content.hasPrefix("a:") || content.hasPrefix(":") {
            let parts = content.split(separator: ":")
            if parts.count >= 2 { return (":\(parts[parts.count - 2]):", nil, end) }
        }
        if content.hasPrefix("t:") {
            let parts = content.split(separator: ":")
            if parts.count >= 2, let timestamp = TimeInterval(parts[1]) {
                return (Date(timeIntervalSince1970: timestamp).formatted(), nil, end)
            }
        }
        return nil
    }

    private static func bareURL(in source: String, from start: String.Index) -> (text: String, url: URL?, endIndex: String.Index) {
        var end = start
        while end < source.endIndex, !source[end].isWhitespace, !"<>\"".contains(source[end]) {
            end = source.index(after: end)
        }
        var address = String(source[start..<end])
        while let last = address.last, ".,;!?".contains(last) { address.removeLast() }
        let consumedEnd = source.index(start, offsetBy: address.count)
        return (address, safeURL(address), consumedEnd)
    }

    private static func safeURL(_ address: String) -> URL? {
        guard let url = URL(string: address), ["http", "https"].contains(url.scheme?.lowercased() ?? "") else { return nil }
        return url
    }
}
