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
    var id: String { url.path }
}

struct EngineLogLine: Decodable, Identifiable, Sendable {
    let level: String
    let message: String
    var id: String { level + message }
}

enum SidebarSection: String, CaseIterable, Identifiable {
    case install
    case mods
    case logs

    var id: Self { self }
    var titleKey: String {
        switch self {
        case .install: "설치"
        case .mods: "모드"
        case .logs: "로그"
        }
    }
    var symbol: String {
        switch self {
        case .install: "shippingbox"
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
