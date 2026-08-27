import AppKit
import SwiftUI

@MainActor
final class AppModel: ObservableObject {
    @Published var selection: SidebarSection? = .install
    @Published var gameURL: URL?
    @Published var response: EngineResponse?
    @Published var isWorking = false
    @Published var activityKey: String?
    @Published var errorKey: String?
    @Published var logs: [EngineLogLine] = []
    @Published var gameWasSelectedManually = false
    @Published var pendingModImport: PendingModImport?
    @Published var isInspectingMod = false
    @Published var gameLogText = ""
    @Published var gameLogExists = false
    @Published var isRefreshingGameLog = false
    @Published var ummLogText = ""
    @Published var ummLogExists = false
    @Published var isRefreshingUMMLog = false

    private let engine = EngineClient()
    private let localization: LocalizationController
    private var queuedImportURL: URL?
    private var didStart = false

    init(localization: LocalizationController) {
        self.localization = localization
    }

    var status: InstallStatus? { response?.status }
    var mods: [InstalledMod] { response?.mods ?? [] }
    var isInstalled: Bool {
        status?.hookInstalled == true && status?.managerInstalled == true
    }
    var gameLocationDescriptionKey: String {
        gameWasSelectedManually ? "직접 선택함" : "Steam에서 찾음"
    }

    func start() async {
        guard !didStart else { return }
        didStart = true
        gameURL = SteamLocator.locate()
        gameWasSelectedManually = false
        await refresh(includeMods: true)
        await inspectQueuedModIfPossible()
    }

    func chooseGame() {
        let panel = NSOpenPanel()
        panel.title = localization.string("얼불춤 앱 선택")
        panel.prompt = localization.string("선택")
        panel.allowedContentTypes = [.application]
        panel.allowsMultipleSelection = false
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        guard SteamLocator.validate(url) else {
            errorKey = "올바른 ADanceOfFireAndIce.app이 아닙니다."
            return
        }
        gameURL = url
        gameWasSelectedManually = true
        Task {
            await refresh(includeMods: true)
            await inspectQueuedModIfPossible()
        }
    }

    func refresh(includeMods: Bool = false) async {
        await perform(action: includeMods ? "mods" : "status", labelKey: "새로 고침 중…")
    }

    func install(repair: Bool = false) async {
        await perform(
            action: "install",
            forcePayload: repair,
            labelKey: repair ? "설치 상태 확인 중…" : "UMM 설치 중…"
        )
        if errorKey == nil { await refresh(includeMods: true) }
    }

    func removeHook() async {
        await perform(action: "remove", labelKey: "UMM 제거 중…")
    }

    func restoreOriginal() async {
        await perform(action: "restore", labelKey: "설치 전 상태로 되돌리는 중…")
    }

    func chooseMod() {
        let panel = NSOpenPanel()
        panel.title = localization.string("추가할 모드 선택")
        panel.allowedContentTypes = [.zip]
        panel.allowsMultipleSelection = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        offerModFile(url)
    }

    func offerModFile(_ url: URL) {
        guard url.isFileURL, url.pathExtension.caseInsensitiveCompare("zip") == .orderedSame else {
            errorKey = "ZIP 형식의 UMM 모드를 선택해 주세요."
            return
        }
        selection = .mods
        queuedImportURL = url
        Task { await inspectQueuedModIfPossible() }
    }

    func confirmModInstall(_ pending: PendingModImport) async {
        pendingModImport = nil
        let installed = await perform(
            action: "installmod",
            zipPath: pending.url.path,
            labelKey: pending.inspection.alreadyInstalled ? "모드 교체 중…" : "모드 추가 중…"
        )
        if installed { await refresh(includeMods: true) }
    }

    func cancelModInstall() {
        pendingModImport = nil
    }

    func removeMod(_ mod: InstalledMod) async {
        await perform(action: "removemod", path: mod.path, labelKey: "모드 제거 중…")
        await refresh(includeMods: true)
    }

    func setMod(_ mod: InstalledMod, enabled: Bool) async {
        await perform(
            action: "setmodenabled",
            modId: mod.id,
            enabled: enabled,
            labelKey: enabled ? "모드 켜는 중…" : "모드 끄는 중…"
        )
        if errorKey == nil { await refresh(includeMods: true) }
    }

    func openModsFolder() {
        guard let path = status?.modsPath else { return }
        NSWorkspace.shared.open(URL(filePath: path))
    }

    func refreshGameLog() async {
        guard !isRefreshingGameLog else { return }
        isRefreshingGameLog = true
        defer { isRefreshingGameLog = false }
        let url = GameLogConfiguration.playerLogURL
        gameLogExists = FileManager.default.fileExists(atPath: url.path)
        guard gameLogExists else {
            gameLogText = ""
            return
        }
        do {
            gameLogText = try await Task.detached {
                try LogReader.tail(of: url, lineLimit: GameLogConfiguration.lineLimit)
            }.value
        } catch {
            gameLogText = ""
            errorKey = "게임 로그를 읽지 못했습니다."
            logs.append(.init(level: "error", message: String(reflecting: error)))
        }
    }

    func openGameLog() {
        reveal(GameLogConfiguration.playerLogURL)
    }

    func refreshUMMLog() async {
        guard !isRefreshingUMMLog, let gameURL else { return }
        isRefreshingUMMLog = true
        defer { isRefreshingUMMLog = false }
        do {
            let result = try await engine.run(EngineRequest(action: "logtail", gamePath: gameURL.path))
            ummLogText = result.logText ?? ""
            ummLogExists = result.logExists == true
        } catch {
            ummLogText = ""
            errorKey = "UMM 로그를 읽지 못했습니다."
            logs.append(.init(level: "error", message: String(reflecting: error)))
        }
    }

    func openUMMLog() {
        guard let managedPath = status?.managedPath else { return }
        reveal(URL(filePath: managedPath).appending(path: "UnityModManager/Log.txt"))
    }

    private func reveal(_ url: URL) {
        guard FileManager.default.fileExists(atPath: url.path) else { return }
        NSWorkspace.shared.activateFileViewerSelecting([url])
    }

    private func inspectQueuedModIfPossible() async {
        guard !isInspectingMod, pendingModImport == nil, let url = queuedImportURL else { return }
        guard let gameURL else {
            if didStart { errorKey = "모드를 추가하려면 먼저 게임을 선택해 주세요." }
            return
        }
        queuedImportURL = nil
        isInspectingMod = true
        defer { isInspectingMod = false }
        do {
            let result = try await engine.run(EngineRequest(
                action: "inspectmod",
                gamePath: gameURL.path,
                zipPath: url.path
            ))
            logs.append(contentsOf: result.log ?? [])
            guard result.ok, let inspection = result.modInspection else {
                if let diagnostic = result.error {
                    logs.append(.init(level: "error", message: diagnostic))
                }
                errorKey = "이 파일은 설치할 수 있는 UMM 모드가 아닙니다."
                return
            }
            pendingModImport = PendingModImport(url: url, inspection: inspection)
        } catch {
            errorKey = "이 파일은 설치할 수 있는 UMM 모드가 아닙니다."
            let diagnostic = (error as? EngineClientError)?.diagnosticDescription
                ?? String(reflecting: error)
            logs.append(.init(level: "error", message: diagnostic))
        }
    }

    @discardableResult
    private func perform(
        action: String,
        forcePayload: Bool = false,
        zipPath: String? = nil,
        path: String? = nil,
        modId: String? = nil,
        enabled: Bool? = nil,
        labelKey: String
    ) async -> Bool {
        guard let gameURL else {
            errorKey = "Steam에서 얼불춤을 찾지 못했습니다. 앱을 직접 선택해 주세요."
            return false
        }
        isWorking = true
        activityKey = labelKey
        errorKey = nil
        defer { isWorking = false; activityKey = nil }

        do {
            let request = EngineRequest(
                action: action,
                gamePath: gameURL.path,
                payloadDir: action == "install" ? bundledPayloadPath : nil,
                zipPath: zipPath,
                path: path,
                modId: modId,
                enabled: enabled,
                forcePayload: forcePayload
            )
            let result = try await engine.run(request)
            response = result
            logs.append(contentsOf: result.log ?? [])
            if !result.ok {
                if let diagnostic = result.error {
                    logs.append(.init(level: "error", message: diagnostic))
                }
                errorKey = friendlyFailureKey(for: action)
                return false
            }
            return true
        } catch {
            errorKey = (error as? EngineClientError)?.userMessageKey
                ?? "작업을 완료하지 못했습니다. 다시 시도해 주세요."
            let diagnostic = (error as? EngineClientError)?.diagnosticDescription
                ?? String(reflecting: error)
            logs.append(.init(level: "error", message: diagnostic))
            return false
        }
    }

    private var bundledPayloadPath: String? {
        let url = Bundle.main.bundleURL.appending(path: "Contents/Resources/UMMPayload")
        return FileManager.default.fileExists(atPath: url.path) ? url.path : nil
    }

    private func friendlyFailureKey(for action: String) -> String {
        switch action {
        case "install": "UMM 설치를 완료하지 못했습니다. 다시 시도해 주세요."
        case "remove": "UMM 제거를 완료하지 못했습니다. 다시 시도해 주세요."
        case "restore": "설치 전 상태로 되돌리지 못했습니다. 다시 시도해 주세요."
        case "installmod": "모드를 추가하지 못했습니다. 파일을 확인하고 다시 시도해 주세요."
        case "removemod", "uninstallmod": "모드를 제거하지 못했습니다. 다시 시도해 주세요."
        case "setmodenabled": "모드 상태를 변경하지 못했습니다. 다시 시도해 주세요."
        default: "작업을 완료하지 못했습니다. 다시 시도해 주세요."
        }
    }
}
