import Foundation
import SwiftUI

struct EngineRequest: Encodable, Sendable {
    let protocolVersion = 1
    let action: String
    var gamePath: String? = nil
    var payloadDir: String? = nil
    var zipPath: String? = nil
    var path: String? = nil
    var modId: String? = nil
    var enabled: Bool? = nil
    var forcePayload = false
}

struct EngineResponse: Decodable, Sendable {
    let ok: Bool
    let detected: Bool
    var error: String?
    var bundledVersion: String?
    var status: InstallStatus?
    var mods: [InstalledMod]?
    var log: [EngineLogLine]?
    var logText: String?
    var logExists: Bool?
    var modInspection: ModInspection?
}

struct InstallStatus: Decodable, Sendable {
    var gameRoot: String?
    var appPath: String?
    var managedPath: String?
    var modsPath: String?
    var processArch: String?
    var gameArch: String?
    var hookInstalled: Bool?
    var managerInstalled: Bool?
    var managerVersion: String?
    var hasBackup: Bool?
    var backupPath: String?
    var warning: String?
}

struct InstalledMod: Decodable, Identifiable, Sendable {
    let id: String
    let name: String
    let version: String
    var managerVersion: String?
    var homePage: String?
    let path: String
    let status: String
    let installed: Bool
    let enabled: Bool
}

struct ModInspection: Decodable, Sendable {
    let id: String
    let name: String
    let version: String
    let alreadyInstalled: Bool
}

struct PendingModImport: Identifiable, Sendable {
    let url: URL
    let inspection: ModInspection
    let deleteWhenFinished: Bool
    var id: String { url.path }
}

struct RemoteMod: Decodable, Identifiable, Sendable, Equatable {
    let id: String
    let name: String
    let version: String?
    let description: String
    let cachedUsername: String
    let uploadedTimestamp: Int64
    let parsedDownload: String?
    let download: String?
    let imageURL: String?
    let hideFromSearch: Bool

    var preferredDownloadURL: URL? {
        [parsedDownload, download]
            .compactMap { $0 }
            .compactMap(URL.init(string:))
            .first { $0.scheme?.lowercased() == "https" }
    }

    var action: RemoteModAction {
        guard let url = preferredDownloadURL else { return .openWebsite }
        if url.pathExtension.caseInsensitiveCompare("zip") == .orderedSame {
            return .downloadAndInstall
        }

        let host = url.host?.lowercased() ?? ""
        let path = url.path.lowercased()
        if host == "youtu.be" || host.hasSuffix("youtube.com") {
            return .openWebsite
        }
        if host == "github.com" && !path.contains("/releases/download/") {
            return .openWebsite
        }
        if ["htm", "html"].contains(url.pathExtension.lowercased()) {
            return .openWebsite
        }
        return .downloadAndInspect
    }

    func matches(searchQuery query: String) -> Bool {
        let query = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return true }
        return [name, cachedUsername, description]
            .contains { $0.localizedStandardContains(query) }
    }

    private enum CodingKeys: String, CodingKey {
        case id, name, version, description, cachedUsername, uploadedTimestamp
        case parsedDownload, download, imageURL, hideFromSearch
    }

    init(from decoder: any Decoder) throws {
        let values = try decoder.container(keyedBy: CodingKeys.self)
        id = try values.decode(String.self, forKey: .id)
        name = try values.decode(String.self, forKey: .name)
        version = try values.decodeIfPresent(String.self, forKey: .version)
        description = try values.decodeIfPresent(String.self, forKey: .description) ?? ""
        cachedUsername = try values.decodeIfPresent(String.self, forKey: .cachedUsername) ?? ""
        uploadedTimestamp = try values.decodeIfPresent(Int64.self, forKey: .uploadedTimestamp) ?? 0
        parsedDownload = try values.decodeIfPresent(String.self, forKey: .parsedDownload)
        download = try values.decodeIfPresent(String.self, forKey: .download)
        imageURL = try values.decodeIfPresent(String.self, forKey: .imageURL)
        hideFromSearch = try values.decodeIfPresent(Bool.self, forKey: .hideFromSearch) ?? false
    }

    init(
        id: String,
        name: String,
        version: String? = nil,
        description: String = "",
        cachedUsername: String = "",
        uploadedTimestamp: Int64 = 0,
        parsedDownload: String? = nil,
        download: String? = nil,
        imageURL: String? = nil,
        hideFromSearch: Bool = false
    ) {
        self.id = id
        self.name = name
        self.version = version
        self.description = description
        self.cachedUsername = cachedUsername
        self.uploadedTimestamp = uploadedTimestamp
        self.parsedDownload = parsedDownload
        self.download = download
        self.imageURL = imageURL
        self.hideFromSearch = hideFromSearch
    }
}

enum RemoteModAction: Sendable, Equatable {
    case downloadAndInstall
    case openWebsite
    case downloadAndInspect
}

struct EngineLogLine: Decodable, Identifiable, Sendable {
    let level: String
    let message: String
    var id: String { level + message }
}

enum SidebarSection: String, CaseIterable, Identifiable {
    case install
    case catalog
    case mods
    case logs

    var id: Self { self }
    var titleKey: String {
        switch self {
        case .install: "설치"
        case .catalog: "모드 찾기"
        case .mods: "모드"
        case .logs: "로그"
        }
    }
    var symbol: String {
        switch self {
        case .install: "shippingbox"
        case .catalog: "magnifyingglass"
        case .mods: "puzzlepiece.extension"
        case .logs: "text.page"
        }
    }
}

enum LogSource: String, CaseIterable, Identifiable {
    case game
    case umm

    static let defaultSource: Self = .game

    var id: Self { self }
    var titleKey: String {
        switch self {
        case .game: "게임 로그"
        case .umm: "UMM 로그"
        }
    }
}
