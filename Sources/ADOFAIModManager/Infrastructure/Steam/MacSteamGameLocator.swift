import Foundation

struct MacSteamGameLocator: GameLocating {
    static let appID = "977950"
    static let appName = "ADanceOfFireAndIce.app"

    func locate() -> URL? {
        Self.locate(fileManager: .default)
    }

    func validate(_ url: URL) -> Bool {
        Self.validate(url, fileManager: .default)
    }

    static func locate(fileManager: FileManager = .default) -> URL? {
        candidateLibraries(fileManager: fileManager)
            .map { $0.appending(path: "steamapps/common/A Dance of Fire and Ice/\(appName)") }
            .first { validate($0, fileManager: fileManager) }
    }

    static func candidateLibraries(fileManager: FileManager = .default) -> [URL] {
        let steam = fileManager.homeDirectoryForCurrentUser
            .appending(path: "Library/Application Support/Steam")
        var libraries = [steam]
        let vdf = steam.appending(path: "steamapps/libraryfolders.vdf")

        if let text = try? String(contentsOf: vdf, encoding: .utf8),
           let regex = try? NSRegularExpression(pattern: #""path"\s+"([^"]+)""#) {
            let range = NSRange(text.startIndex..., in: text)
            for match in regex.matches(in: text, range: range) {
                guard let pathRange = Range(match.range(at: 1), in: text) else { continue }
                let path = String(text[pathRange]).replacingOccurrences(of: #"\\"#, with: #""#)
                libraries.append(URL(filePath: path))
            }
        }

        return libraries.reduce(into: []) { result, item in
            if !result.contains(item) { result.append(item) }
        }
    }

    static func validate(_ appURL: URL, fileManager: FileManager = .default) -> Bool {
        let managed = appURL
            .appending(path: "Contents/Resources/Data/Managed/UnityEngine.CoreModule.dll")
        return appURL.pathExtension == "app" && fileManager.fileExists(atPath: managed.path)
    }
}
