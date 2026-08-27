import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController
    @State private var isDropTargeted = false

    var body: some View {
        NavigationSplitView {
            List(SidebarSection.allCases, selection: $model.selection) { section in
                Label(localization.string(section.titleKey), systemImage: section.symbol)
                    .tag(section)
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 210)
        } detail: {
            Group {
                switch model.selection ?? .install {
                case .install:
                    InstallView()
                case .mods:
                    ModsView()
                case .logs:
                    LogsView()
                }
            }
        }
        .dropDestination(for: URL.self) { urls, _ in
            guard let zip = urls.first(where: {
                $0.isFileURL && $0.pathExtension.caseInsensitiveCompare("zip") == .orderedSame
            }) else { return false }
            model.offerModFile(zip)
            return true
        } isTargeted: { isTargeted in
            isDropTargeted = isTargeted
        }
        .overlay {
            if isDropTargeted {
                ZStack {
                    Color.accentColor.opacity(0.10)
                    VStack(spacing: LayoutMetrics.rowSpacing) {
                        Image(systemName: "shippingbox.and.arrow.backward.fill")
                            .font(.system(size: 38))
                        Text(verbatim: localization.string("모드 ZIP을 여기에 놓으세요"))
                            .font(.title2.bold())
                        Text(verbatim: localization.string("놓은 뒤 내용을 확인하고 설치 여부를 묻습니다."))
                            .foregroundStyle(.secondary)
                    }
                    .padding(LayoutMetrics.pageVertical)
                    .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 20))
                }
                .overlay {
                    RoundedRectangle(cornerRadius: 18)
                        .stroke(Color.accentColor, style: StrokeStyle(lineWidth: 3, dash: [10, 7]))
                        .padding(LayoutMetrics.compact)
                }
                .allowsHitTesting(false)
            }
        }
        .alert(localization.string("작업을 완료하지 못했습니다"), isPresented: Binding(
            get: { model.errorKey != nil },
            set: { if !$0 { model.errorKey = nil } }
        )) {
            Button(localization.string("확인"), role: .cancel) {}
        } message: {
            Text(verbatim: model.errorKey.map { localization.string($0) } ?? "")
        }
        .alert(item: $model.pendingModImport) { pending in
            Alert(
                title: Text(verbatim: localization.string(
                    pending.inspection.alreadyInstalled
                        ? "“%@” 모드를 교체하시겠습니까?"
                        : "“%@” 모드를 설치하시겠습니까?",
                    pending.inspection.name
                )),
                message: Text(verbatim: pending.inspection.version.isEmpty
                    ? localization.string("확인하면 모드 폴더에 추가됩니다.")
                    : localization.string("버전 %@ · 확인하면 모드 폴더에 추가됩니다.", pending.inspection.version)),
                primaryButton: .default(Text(verbatim: localization.string(
                    pending.inspection.alreadyInstalled ? "교체" : "설치"
                ))) {
                    Task { await model.confirmModInstall(pending) }
                },
                secondaryButton: .cancel(Text(verbatim: localization.string("취소"))) {
                    model.cancelModInstall()
                }
            )
        }
    }
}

private struct InstallView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: LayoutMetrics.sectionSpacing) {
                PageTitle(
                    title: "ADOFAI Mod Manager",
                    subtitle: localization.string("얼불춤에 UMM을 설치하고 모드를 관리합니다.")
                )

                statusCard
                actions

                if model.status?.warning != nil {
                    Label {
                        Text(verbatim: localization.string(
                            "설치 상태를 확인해야 합니다. ‘설치 문제 해결’을 실행해 주세요."
                        ))
                    } icon: {
                        Image(systemName: "exclamationmark.triangle.fill")
                    }
                    .foregroundStyle(.orange)
                    .padding(LayoutMetrics.cardPadding)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(
                        .orange.opacity(0.09),
                        in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius)
                    )
                }
            }
            .frame(maxWidth: LayoutMetrics.readableContentWidth, alignment: .leading)
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.vertical, LayoutMetrics.pageVertical)
            .frame(maxWidth: .infinity, alignment: .top)
        }
    }

    private var statusCard: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.cardPadding) {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                Label {
                    Text(verbatim: localization.string("설치 상태"))
                        .font(.headline)
                } icon: {
                    Image(systemName: model.isInstalled ? "checkmark.seal.fill" : "circle.dashed")
                        .foregroundStyle(model.isInstalled ? .green : .secondary)
                }
                Spacer()
                Button {
                    Task { await model.refresh(includeMods: true) }
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .buttonStyle(.borderless)
                .disabled(model.isWorking)
            }

            Divider()

            HStack(alignment: .center, spacing: LayoutMetrics.rowSpacing) {
                VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                    Text(verbatim: localization.string("얼불춤"))
                        .foregroundStyle(.secondary)
                    Text(verbatim: localization.string(
                        model.gameURL == nil ? "찾지 못함" : model.gameLocationDescriptionKey
                    ))
                        .foregroundStyle(model.gameURL == nil ? .secondary : .primary)
                }

                Spacer(minLength: LayoutMetrics.sectionSpacing)

                Button(action: model.chooseGame) {
                    Label(localization.string(
                        model.gameURL == nil ? "게임 선택" : "게임 폴더 다시 선택"
                    ), systemImage: "folder")
                }
            }
            VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                Text(verbatim: "UMM")
                    .foregroundStyle(.secondary)
                Text(verbatim: model.status?.managerVersion.map {
                    localization.string("설치됨 · 버전 %@", $0)
                } ?? localization.string("설치 필요"))
                    .foregroundStyle(model.status?.managerVersion == nil ? .secondary : .primary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(LayoutMetrics.cardPadding)
        .adaptiveGlass()
    }

    private var actions: some View {
        HStack(spacing: LayoutMetrics.rowSpacing) {
            Button {
                Task { await model.install(repair: model.isInstalled) }
            } label: {
                HStack(spacing: LayoutMetrics.compact) {
                    if model.isWorking {
                        ProgressView()
                            .controlSize(.small)
                            .accessibilityHidden(true)
                    } else {
                        Image(systemName: model.isInstalled ? "arrow.clockwise" : "arrow.down.app")
                    }
                    Text(verbatim: primaryActionTitle)
                }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(model.gameURL == nil || model.isWorking)

            Menu {
                Button {
                    Task { await model.install(repair: true) }
                } label: {
                    Label(localization.string("설치 문제 해결"), systemImage: "wrench.and.screwdriver")
                }

                Divider()

                Button(role: .destructive) {
                    Task { await model.removeHook() }
                } label: {
                    Label(localization.string("UMM 제거"), systemImage: "trash")
                }
                .disabled(model.status?.hookInstalled != true)

                Button {
                    Task { await model.restoreOriginal() }
                } label: {
                    Label(localization.string("설치 전 상태로 되돌리기"), systemImage: "arrow.uturn.backward")
                }
                .disabled(model.status?.hasBackup != true)
            } label: {
                Label(localization.string("더 보기"), systemImage: "ellipsis.circle")
            }
            .menuStyle(.borderlessButton)
            .fixedSize()
            .disabled(model.gameURL == nil || model.isWorking)

            Spacer(minLength: 0)
        }
    }

    private var primaryActionTitle: String {
        if model.isWorking {
            return model.activityKey.map { localization.string($0) } ?? ""
        }
        return localization.string(model.isInstalled ? "다시 설치" : "UMM 설치")
    }
}

private struct ModsView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: LayoutMetrics.rowSpacing) {
                PageTitle(
                    title: localization.string("모드"),
                    subtitle: localization.installedModCount(model.mods.count)
                )
                Spacer()
                Menu {
                    Button {
                        Task { await model.refresh(includeMods: true) }
                    } label: {
                        Label(localization.string("목록 새로 고침"), systemImage: "arrow.clockwise")
                    }
                    .disabled(model.isWorking)

                    Button {
                        model.openModsFolder()
                    } label: {
                        Label(localization.string("모드 폴더 열기"), systemImage: "folder")
                    }
                    .disabled(model.status?.modsPath == nil)
                } label: {
                    Label(localization.string("목록 관리"), systemImage: "ellipsis.circle")
                }
                .menuStyle(.borderlessButton)
                .fixedSize()

                Button {
                    model.chooseMod()
                } label: {
                    if model.isInspectingMod {
                        HStack(spacing: LayoutMetrics.compact) {
                            ProgressView().controlSize(.small)
                            Text(verbatim: localization.string("모드 확인 중…"))
                        }
                    } else {
                        Label(localization.string("모드 추가"), systemImage: "plus")
                    }
                }
                .buttonStyle(.borderedProminent)
                .disabled(model.isInspectingMod || model.isWorking)
            }
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.top, LayoutMetrics.pageVertical)
            .padding(.bottom, LayoutMetrics.cardPadding)

            Divider()

            if model.mods.isEmpty {
                ContentUnavailableView(
                    localization.string("설치된 모드가 없습니다"),
                    systemImage: "puzzlepiece.extension",
                    description: Text(verbatim: localization.string(
                        "모드 추가 버튼으로 UMM 모드를 추가할 수 있습니다."
                    ))
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 0) {
                        ForEach(model.mods) { mod in
                            ModRow(mod: mod)
                            if mod.id != model.mods.last?.id {
                                Divider()
                                    .padding(.leading, LayoutMetrics.pageHorizontal + 32)
                            }
                        }
                    }
                }
            }
        }
    }
}

private struct ModRow: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController
    let mod: InstalledMod

    var body: some View {
        HStack(spacing: LayoutMetrics.rowSpacing) {
            Toggle(localization.string(mod.enabled ? "모드 켜짐" : "모드 꺼짐"), isOn: Binding(
                get: { mod.enabled },
                set: { newValue in Task { await model.setMod(mod, enabled: newValue) } }
            ))
            .toggleStyle(.checkbox)
            .labelsHidden()
            .help(localization.string(mod.enabled ? "모드 끄기" : "모드 켜기"))
            .disabled(!mod.installed || model.isWorking)

            VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                Text(mod.name)
                    .font(.headline)
                Text(verbatim: "\(mod.id) · \(mod.version)")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Spacer(minLength: LayoutMetrics.sectionSpacing)

            Button(role: .destructive) {
                Task { await model.removeMod(mod) }
            } label: {
                Label(localization.string("모드 제거"), systemImage: "trash")
                    .labelStyle(.iconOnly)
            }
            .buttonStyle(.borderless)
            .help(localization.string("모드 제거"))
            .disabled(model.isWorking)
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal)
        .padding(.vertical, LayoutMetrics.rowSpacing)
    }
}

private struct LogsView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController
    @State private var source = LogSource.defaultSource

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: LayoutMetrics.sectionSpacing) {
                PageTitle(
                    title: localization.string("로그"),
                    subtitle: localization.string(
                        source == .game ? "게임 실행 기록" : "UMM 작업 기록"
                    )
                )
                Spacer()
                Picker(localization.string("로그 종류"), selection: $source) {
                    ForEach(LogSource.allCases) { item in
                        Text(verbatim: localization.string(item.titleKey))
                            .tag(item)
                    }
                }
                .pickerStyle(.segmented)
                .labelsHidden()
                .frame(width: 240)
            }
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.top, LayoutMetrics.pageVertical)
            .padding(.bottom, LayoutMetrics.cardPadding)

            Divider()

            switch source {
            case .game:
                GameLogView()
            case .umm:
                UMMLogView()
            }
        }
    }
}

private struct GameLogView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.rowSpacing) {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                Label(localization.string("최근 30줄"), systemImage: "gamecontroller")
                    .font(.headline)
                Spacer()
                Button {
                    Task { await model.refreshGameLog() }
                } label: {
                    if model.isRefreshingGameLog {
                        ProgressView().controlSize(.small)
                    } else {
                        Label(localization.string("새로 고침"), systemImage: "arrow.clockwise")
                    }
                }
                .disabled(model.isRefreshingGameLog)
                .help(localization.string("게임 로그 새로 고침"))

                Button {
                    model.openGameLog()
                } label: {
                    Label(localization.string("Finder에서 보기"), systemImage: "folder")
                }
                .disabled(!model.gameLogExists)
            }

            LogSurface {
                LogTextOrEmpty(
                    text: model.gameLogText,
                    title: localization.string("아직 표시할 게임 로그가 없습니다"),
                    description: localization.string("게임을 한 번 실행하면 로그가 생성됩니다.")
                )
            }
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal)
        .padding(.vertical, LayoutMetrics.cardPadding)
        .task { await model.refreshGameLog() }
    }
}

private struct UMMLogView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.rowSpacing) {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                Label(localization.string("UMM 로그"), systemImage: "wrench.and.screwdriver")
                    .font(.headline)
                Spacer()
                Button {
                    Task { await model.refreshUMMLog() }
                } label: {
                    if model.isRefreshingUMMLog {
                        ProgressView().controlSize(.small)
                    } else {
                        Label(localization.string("새로 고침"), systemImage: "arrow.clockwise")
                    }
                }
                .disabled(model.isRefreshingUMMLog || model.gameURL == nil)

                Button {
                    model.openUMMLog()
                } label: {
                    Label(localization.string("Finder에서 보기"), systemImage: "folder")
                }
                .disabled(!model.ummLogExists)
            }

            LogSurface {
                LogTextOrEmpty(
                    text: model.ummLogText,
                    title: localization.string("아직 UMM 로그가 없습니다"),
                    description: localization.string("게임에서 UMM이 실행되면 로그가 생성됩니다.")
                )
            }
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal)
        .padding(.vertical, LayoutMetrics.cardPadding)
        .task { await model.refreshUMMLog() }
    }
}

private struct LogTextOrEmpty: View {
    let text: String
    let title: String
    let description: String

    var body: some View {
        if text.isEmpty {
            ContentUnavailableView(
                title,
                systemImage: "text.page",
                description: Text(verbatim: description)
            )
        } else {
            ScrollView([.horizontal, .vertical]) {
                Text(verbatim: text)
                    .font(.system(.body, design: .monospaced))
                    .textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(LayoutMetrics.cardPadding)
            }
        }
    }
}

private struct LogSurface<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        content
            .frame(maxWidth: .infinity, minHeight: 340, maxHeight: .infinity)
            .background(
                Color(nsColor: .textBackgroundColor),
                in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius)
            )
            .overlay {
                RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius)
                    .stroke(.separator.opacity(0.55), lineWidth: 1)
            }
    }
}

private struct PageTitle: View {
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
            Text(verbatim: title)
                .font(.largeTitle.bold())
            Text(verbatim: subtitle)
                .font(.title3)
                .foregroundStyle(.secondary)
        }
    }
}

private extension View {
    @ViewBuilder
    func adaptiveGlass() -> some View {
        if #available(macOS 26.0, *) {
            self.glassEffect(
                .regular,
                in: .rect(cornerRadius: LayoutMetrics.cardCornerRadius)
            )
        } else {
            self.background(
                .regularMaterial,
                in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius)
            )
        }
    }
}
