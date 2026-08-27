import Foundation
import Testing
@testable import ADOFAIModManager

@Test func validatesADOFAIApplicationLayout() throws {
    let root = FileManager.default.temporaryDirectory
        .appending(path: UUID().uuidString)
        .appending(path: "ADanceOfFireAndIce.app")
    let managed = root.appending(path: "Contents/Resources/Data/Managed")
    try FileManager.default.createDirectory(at: managed, withIntermediateDirectories: true)
    let core = managed.appending(path: "UnityEngine.CoreModule.dll")
    FileManager.default.createFile(atPath: core.path, contents: Data())
    defer { try? FileManager.default.removeItem(at: root.deletingLastPathComponent()) }

    #expect(MacSteamGameLocator.validate(root))
}

@Test func rejectsUnrelatedApplication() {
    let url = URL(filePath: "/Applications/NotADOFAI.app")
    #expect(!MacSteamGameLocator.validate(url))
}

@Test func layoutUsesSharedEightPointSystem() {
    #expect(LayoutMetrics.unit == 8)
    #expect(LayoutMetrics.compact == 8)
    #expect(LayoutMetrics.rowSpacing == 12)
    #expect(LayoutMetrics.sectionSpacing == 24)
    #expect(LayoutMetrics.pageHorizontal == 24)
    #expect(LayoutMetrics.pageVertical == 28)
    #expect(LayoutMetrics.cardPadding == 20)
    #expect(LayoutMetrics.cardCornerRadius == 16)
}

@Test func gameLogDefaultsAndFutureContractAreStable() {
    #expect(LogSource.defaultSource == .game)
    #expect(GameLogConfiguration.lineLimit == 30)
    #expect(GameLogConfiguration.playerLogURL.lastPathComponent == "Player.log")
    #expect(GameLogConfiguration.playerLogURL.path.contains(
        "Library/Logs/7th Beat Games/A Dance of Fire and Ice"
    ))
}

@Test func mainUIAvoidsInternalTerminology() throws {
    let source = try uiSource()

    for forbidden in ["후크", "아키텍처", "설치 엔진", "CoreModule", "Managed"] {
        #expect(!source.contains(forbidden))
    }
}

@Test func contextualActionsAreNotPlacedInTheGlobalToolbar() throws {
    let source = try uiSource()

    #expect(!source.contains(".toolbar {"))
    #expect(source.contains("게임 폴더 다시 선택"))
    #expect(source.contains("목록 관리"))
    #expect(source.contains("목록 새로 고침"))
}

@Test func workProgressAppearsInsideThePrimaryAction() throws {
    let source = try uiSource()

    #expect(!source.contains(".overlay(alignment: .bottom)"))
    #expect(source.contains("ProgressView()"))
    #expect(source.contains("Text(verbatim: primaryActionTitle)"))
}

@Test func installStatusDetailsUseOneGroupedLayout() throws {
    let source = try uiSource()

    #expect(!source.contains("StatusRow("))
    #expect(source.contains("설치됨 · 버전"))
    #expect(!source.contains("UMM 설치됨 · 버전"))
}

@Test @MainActor func appLanguageDefaultsPersistsAndRecovers() {
    let suiteName = "LocalizationTests.\(UUID().uuidString)"
    let defaults = UserDefaults(suiteName: suiteName)!
    defer { defaults.removePersistentDomain(forName: suiteName) }

    let initial = LocalizationController(defaults: defaults)
    #expect(initial.language == .system)

    initial.select(.simplifiedChinese)
    #expect(defaults.string(forKey: AppLanguage.storageKey) == "zh-Hans")
    #expect(LocalizationController(defaults: defaults).language == .simplifiedChinese)

    defaults.set("unsupported", forKey: AppLanguage.storageKey)
    #expect(LocalizationController(defaults: defaults).language == .system)
}

@Test @MainActor func localizedStringsAndFormatsResolveForEveryLanguage() {
    let suiteName = "LocalizationFormats.\(UUID().uuidString)"
    let defaults = UserDefaults(suiteName: suiteName)!
    defer { defaults.removePersistentDomain(forName: suiteName) }
    let localization = LocalizationController(defaults: defaults)

    localization.select(.korean)
    #expect(localization.string("설치") == "설치")
    #expect(localization.installedModCount(2) == "2개 설치됨")

    localization.select(.english)
    #expect(localization.string("설치") == "Install")
    #expect(localization.installedModCount(1) == "1 mod installed")
    #expect(localization.installedModCount(2) == "2 mods installed")
    #expect(localization.string("설치됨 · 버전 %@", "0.32.5.0") == "Installed · Version 0.32.5.0")

    localization.select(.simplifiedChinese)
    #expect(localization.string("설치") == "安装")
    #expect(localization.installedModCount(2) == "已安装 2 个模组")
}

@Test func localizationFilesHaveMatchingUniqueKeys() throws {
    let resources = packageRoot()
        .appending(path: "Sources/ADOFAIModManager/Resources")
    let localizations = ["ko", "en", "zh-Hans"]
    var expectedKeys: Set<String>?

    for language in localizations {
        let url = resources
            .appending(path: "\(language).lproj/Localizable.strings")
        let source = try String(contentsOf: url, encoding: .utf8)
        let keys = source.split(separator: "\n").compactMap { line -> String? in
            guard line.first == "\"", let separator = line.range(of: "\" = ") else {
                return nil
            }
            return String(line[line.index(after: line.startIndex)..<separator.lowerBound])
        }

        #expect(Set(keys).count == keys.count)
        if let expectedKeys {
            #expect(Set(keys) == expectedKeys)
        } else {
            expectedKeys = Set(keys)
        }
    }

    #expect(expectedKeys?.contains("게임 폴더 다시 선택") == true)
    #expect(expectedKeys?.contains("mods.installed_count.one") == true)
    #expect(expectedKeys?.contains("시스템 설정 따르기") == true)
}

@Test func appAdvertisesEverySupportedLocalization() throws {
    let infoPlist = packageRoot().appending(path: "Resources/Info.plist")
    let data = try Data(contentsOf: infoPlist)
    let plist = try #require(
        PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
    )
    let localizations = try #require(plist["CFBundleLocalizations"] as? [String])

    #expect(Set(localizations) == ["ko", "en", "zh-Hans"])
    #expect(plist["CFBundleDevelopmentRegion"] as? String == "ko")

    let documentTypes = try #require(plist["CFBundleDocumentTypes"] as? [[String: Any]])
    let zipType = try #require(documentTypes.first)
    #expect(zipType["CFBundleTypeRole"] as? String == "Viewer")
    #expect(zipType["LSHandlerRank"] as? String == "Alternate")
    #expect(zipType["LSItemContentTypes"] as? [String] == ["public.zip-archive"])
}

@Test func logReaderReturnsOnlyTheRequestedTail() throws {
    let url = FileManager.default.temporaryDirectory
        .appending(path: "LogReader-\(UUID().uuidString).log")
    defer { try? FileManager.default.removeItem(at: url) }
    try (1...45).map(String.init).joined(separator: "\n").write(to: url, atomically: true, encoding: .utf8)

    let tail = try FileLogReader().tail(of: url, lineLimit: 30)
    let lines = tail.split(separator: "\n")
    #expect(lines.count == 30)
    #expect(lines.first == "16")
    #expect(lines.last == "45")
}

@Test func appConnectsEveryModImportEntryPoint() throws {
    let sources = packageRoot().appending(path: "Sources/ADOFAIModManager")
    let content = try uiSource()
    let app = try String(contentsOf: sources.appending(path: "ADOFAIModManagerApp.swift"), encoding: .utf8)

    #expect(content.contains(".dropDestination(for: URL.self)"))
    #expect(content.contains("model.offerModFile(zip)"))
    #expect(app.contains(".onOpenURL { model.offerModFile($0) }"))
}

@Test func finderOpenUsesTheSingleMainWindow() throws {
    let app = try String(
        contentsOf: packageRoot().appending(path: "Sources/ADOFAIModManager/ADOFAIModManagerApp.swift"),
        encoding: .utf8
    )

    #expect(app.contains("Window(\"ADOFAI Mod Manager\", id: \"main\")"))
    #expect(!app.contains("WindowGroup"))
    #expect(app.contains(".onOpenURL { model.offerModFile($0) }"))
}

@Test func modRowsUseOneCompactStateControl() throws {
    let source = try uiSource()

    #expect(source.contains(".toggleStyle(.checkbox)"))
    #expect(!source.contains(".toggleStyle(.switch)"))
    #expect(!source.contains("checkmark.circle.fill"))
}

@Test func removeButtonPermanentlyDeletesMods() throws {
    let source = try String(
        contentsOf: packageRoot().appending(path: "Sources/ADOFAIModManager/AppModel.swift"),
        encoding: .utf8
    )

    #expect(source.contains("perform(action: \"removemod\""))
    #expect(!source.contains("perform(action: \"uninstallmod\""))
}

@Test func removedModBackupsNeverAppearInTheInstalledList() throws {
    let source = try String(
        contentsOf: packageRoot().appending(path: "Engine/NativeUmm.Infrastructure/Mods/ModManager.cs"),
        encoding: .utf8
    )

    #expect(source.contains("AddFrom(result, layout.ModsPath, installed: true)"))
    #expect(!source.contains("AddFrom(result, AppData.RemovedMods, installed: false)"))
}

@Test func dmgBackgroundUsesOneArrowShapeWithoutCaption() throws {
    let source = try String(
        contentsOf: packageRoot().appending(path: "scripts/generate-assets.swift"),
        encoding: .utf8
    )

    #expect(!source.contains("Drag to Applications"))
    #expect(source.contains("arrow.fill()"))
    #expect(!source.contains("arrow.stroke()"))
}

@Test func modConfirmationPassesItsCapturedImportToInstallation() throws {
    let content = try uiSource()
    let model = try String(
        contentsOf: packageRoot().appending(path: "Sources/ADOFAIModManager/AppModel.swift"),
        encoding: .utf8
    )

    #expect(content.contains("confirmModInstall(pending)"))
    #expect(model.contains("func confirmModInstall(_ pending: PendingModImport) async"))
    #expect(model.contains("if installed { await refresh(includeMods: true) }"))
    #expect(!model.contains("guard let pending = pendingModImport else { return }"))
}

@Test func generatedAssetsUseTheProvidedIconMaster() throws {
    let root = packageRoot()
    let generator = try String(
        contentsOf: root.appending(path: "scripts/generate-assets.swift"),
        encoding: .utf8
    )
    let master = root.appending(path: "Resources/AppIconMaster.png")

    #expect(generator.contains("Resources/AppIconMaster.png"))
    #expect(FileManager.default.fileExists(atPath: master.path))
}

@Test func macInstallerBundlesUnityMonoHarmonyBuild() throws {
    let root = packageRoot()
    let installer = try ["Installer.cs", "PayloadResolver.cs"].map {
        try String(
            contentsOf: root.appending(path: "Engine/NativeUmm.Infrastructure/Installation/\($0)"),
            encoding: .utf8
        )
    }.joined(separator: "\n")
    let client = try String(
        contentsOf: root.appending(path: "Sources/ADOFAIModManager/Infrastructure/Engine/ProcessEngineClient.swift"),
        encoding: .utf8
    )
    let buildScript = try String(
        contentsOf: root.appending(path: "scripts/build-app.sh"),
        encoding: .utf8
    )

    #expect(!installer.contains("ApplyHarmonyCompatibilityOverride"))
    #expect(!client.contains("ADOFAI_HARMONY_OVERRIDE"))
    #expect(installer.contains("reference.Version is { Major: >= 5 }"))
    #expect(buildScript.contains("Resources/UMMPayload"))
    #expect(buildScript.contains("Resources/UMMPayload/"))
    #expect(buildScript.contains("lib/net48/0Harmony.dll"))
    #expect(!buildScript.contains("find \"$nuget_root/lib.harmony"))
    #expect(modelSource().contains("payloadDir: action == \"install\" ? bundledPayloadPath : nil"))
}

@Test func reinstallUsesTheRepairPayloadPath() throws {
    let content = try uiSource()

    #expect(content.contains("model.install(repair: model.isInstalled)"))
}

private func packageRoot() -> URL {
    URL(filePath: #filePath)
        .deletingLastPathComponent()
        .deletingLastPathComponent()
        .deletingLastPathComponent()
}

private func modelSource() -> String {
    (try? String(
        contentsOf: packageRoot().appending(path: "Sources/ADOFAIModManager/AppModel.swift"),
        encoding: .utf8
    )) ?? ""
}

private func uiSource() throws -> String {
    let sourceRoot = packageRoot().appending(path: "Sources/ADOFAIModManager")
    return try [
        "UI/ContentView.swift",
        "Features/Installation/InstallationView.swift",
        "Features/Mods/ModsView.swift",
        "Features/Logs/LogsView.swift"
    ].map {
        try String(contentsOf: sourceRoot.appending(path: $0), encoding: .utf8)
    }.joined(separator: "\n")
}
