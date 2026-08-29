import Foundation

enum ModCatalogError: Error, Equatable {
    case invalidResponse
    case insecureURL
    case fileTooLarge
    case notZip
}

enum CatalogDownloadStorage {
    private static let directoryName = "ADOFAIModManagerDownloads"

    static var directoryURL: URL {
        FileManager.default.temporaryDirectory.appending(path: directoryName, directoryHint: .isDirectory)
    }

    static func prepare() throws {
        try FileManager.default.createDirectory(at: directoryURL, withIntermediateDirectories: true)
    }

    static func destinationURL() throws -> URL {
        try prepare()
        return directoryURL.appending(path: UUID().uuidString).appendingPathExtension("zip")
    }

    static func remove(_ url: URL) {
        guard url.deletingLastPathComponent().standardizedFileURL == directoryURL.standardizedFileURL else { return }
        try? FileManager.default.removeItem(at: url)
    }

    static func cleanupAll() {
        try? FileManager.default.removeItem(at: directoryURL)
    }
}

struct ModCatalogClient: ModCatalogServing {
    static let endpoint = URL(string: "https://bot.adofai.gg/api/mods/")!
    static let maximumDownloadBytes: Int64 = 512 * 1024 * 1024

    private let session: URLSession
    private let endpoint: URL

    init(session: URLSession = .shared, endpoint: URL = Self.endpoint) {
        self.session = session
        self.endpoint = endpoint
    }

    func fetchMods() async throws -> [RemoteMod] {
        let (data, response) = try await session.data(from: endpoint)
        try validate(response)
        return try JSONDecoder().decode([RemoteMod].self, from: data)
            .filter { !$0.hideFromSearch }
            .sorted { $0.uploadedTimestamp > $1.uploadedTimestamp }
    }

    func download(_ mod: RemoteMod) async throws -> URL {
        guard let sourceURL = mod.preferredDownloadURL,
              sourceURL.scheme?.lowercased() == "https" else {
            throw ModCatalogError.insecureURL
        }

        let (temporaryURL, response) = try await session.download(from: sourceURL)
        var movedToOwnedStorage = false
        defer {
            if !movedToOwnedStorage {
                try? FileManager.default.removeItem(at: temporaryURL)
            }
        }
        try Task.checkCancellation()
        try validate(response)
        guard response.url?.scheme?.lowercased() == "https" else {
            throw ModCatalogError.insecureURL
        }
        if response.expectedContentLength > Self.maximumDownloadBytes {
            throw ModCatalogError.fileTooLarge
        }

        let fileSize = try FileManager.default.attributesOfItem(atPath: temporaryURL.path)[.size]
            .flatMap { ($0 as? NSNumber)?.int64Value } ?? 0
        guard fileSize <= Self.maximumDownloadBytes else {
            throw ModCatalogError.fileTooLarge
        }
        guard try Self.hasZipSignature(at: temporaryURL) else {
            throw ModCatalogError.notZip
        }

        let destinationURL = try CatalogDownloadStorage.destinationURL()
        try FileManager.default.moveItem(at: temporaryURL, to: destinationURL)
        movedToOwnedStorage = true
        return destinationURL
    }

    private func validate(_ response: URLResponse) throws {
        guard let response = response as? HTTPURLResponse,
              (200..<300).contains(response.statusCode) else {
            throw ModCatalogError.invalidResponse
        }
    }

    private static func hasZipSignature(at url: URL) throws -> Bool {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        let signature = try handle.read(upToCount: 4) ?? Data()
        let accepted: [Data] = [
            Data([0x50, 0x4B, 0x03, 0x04]),
            Data([0x50, 0x4B, 0x05, 0x06]),
            Data([0x50, 0x4B, 0x07, 0x08])
        ]
        return accepted.contains(signature)
    }
}
