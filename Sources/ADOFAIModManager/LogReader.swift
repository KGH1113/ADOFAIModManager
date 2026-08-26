import Foundation

enum LogReader {
    static func tail(of url: URL, lineLimit: Int) throws -> String {
        guard lineLimit > 0 else { return "" }
        let data = try Data(contentsOf: url, options: [.mappedIfSafe])
        let text = String(decoding: data, as: UTF8.self)
        var lines = text.split(separator: "\n", omittingEmptySubsequences: false)
        if lines.last?.isEmpty == true { lines.removeLast() }
        return lines.suffix(lineLimit).joined(separator: "\n")
    }
}
