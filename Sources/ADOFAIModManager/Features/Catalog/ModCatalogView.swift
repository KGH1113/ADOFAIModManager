import SwiftUI

struct ModCatalogView: View {
    @EnvironmentObject private var model: ModCatalogViewModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: LayoutMetrics.rowSpacing) {
                PageTitle(
                    title: localization.string("모드 찾기"),
                    subtitle: localization.string("커뮤니티 모드를 찾아 바로 설치할 수 있습니다.")
                )
                Spacer()
                Button { Task { await model.refresh() } } label: {
                    Label(localization.string("목록 새로 고침"), systemImage: "arrow.clockwise")
                }
                .disabled(model.isLoading || model.downloadingID != nil)
            }
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.top, LayoutMetrics.pageVertical)
            .padding(.bottom, LayoutMetrics.cardPadding)

            Divider()

            HStack(spacing: 0) {
                catalogList
                    .frame(width: LayoutMetrics.catalogListWidth)
                Divider()
                detail
                    .frame(minWidth: 320, maxWidth: .infinity, maxHeight: .infinity)
                    .layoutPriority(1)
            }
        }
        // NavigationSplitView reserves this inset for the macOS sidebar edge.
        // List-backed catalog content otherwise paints over that edge and makes
        // the sidebar appear narrower when this tab is selected.
        .padding(.leading, LayoutMetrics.compact)
        .task { await model.loadIfNeeded() }
        .onChange(of: model.searchText) { _, _ in model.selectFirstVisibleIfNeeded() }
        .alert(item: $model.websiteFallback) { mod in
            Alert(
                title: Text(verbatim: localization.string("이 모드는 앱에서 직접 설치할 수 없습니다.")),
                message: Text(verbatim: localization.string("다운로드 웹사이트를 기본 브라우저에서 여시겠습니까?")),
                primaryButton: .default(Text(verbatim: localization.string("웹사이트 열기"))) {
                    model.openWebsite(for: mod)
                },
                secondaryButton: .cancel(Text(verbatim: localization.string("취소")))
            )
        }
        .alert(localization.string("다운로드할 수 없습니다"), isPresented: Binding(
            get: { model.downloadErrorKey != nil },
            set: { if !$0 { model.downloadErrorKey = nil } }
        )) {
            Button(localization.string("확인"), role: .cancel) {}
        } message: {
            Text(verbatim: model.downloadErrorKey.map { localization.string($0) } ?? "")
        }
    }

    @ViewBuilder
    private var catalogList: some View {
        if model.isLoading && model.mods.isEmpty {
            ProgressView(localization.string("모드 목록 불러오는 중…"))
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        } else if let errorKey = model.loadErrorKey, model.mods.isEmpty {
            ContentUnavailableView {
                Label(localization.string("모드 목록을 불러오지 못했습니다"), systemImage: "wifi.exclamationmark")
            } description: {
                Text(verbatim: localization.string(errorKey))
            } actions: {
                Button(localization.string("다시 시도")) { Task { await model.refresh() } }
            }
        } else if model.mods.isEmpty {
            ContentUnavailableView(
                localization.string("표시할 모드가 없습니다"),
                systemImage: "magnifyingglass"
            )
        } else {
            VStack(spacing: 0) {
                TextField(
                    localization.string("이름, 작성자 또는 설명 검색"),
                    text: $model.searchText
                )
                .textFieldStyle(.roundedBorder)
                .padding(LayoutMetrics.rowSpacing)

                Divider()

                List(model.filteredMods, selection: $model.selectedID) { mod in
                    ModCatalogRow(mod: mod)
                        .tag(mod.id)
                }
                .overlay {
                    if model.filteredMods.isEmpty {
                        ContentUnavailableView(
                            localization.string("검색 결과가 없습니다"),
                            systemImage: "magnifyingglass",
                            description: Text(verbatim: localization.string("다른 검색어를 입력해 보세요."))
                        )
                    }
                }
            }
        }
    }

    @ViewBuilder
    private var detail: some View {
        if let mod = model.selectedMod {
            ModCatalogDetail(mod: mod)
        } else {
            ContentUnavailableView(
                localization.string("모드를 선택하세요"),
                systemImage: "puzzlepiece.extension"
            )
        }
    }
}

private struct ModCatalogRow: View {
    @EnvironmentObject private var localization: LocalizationController
    let mod: RemoteMod

    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
            Text(verbatim: mod.name)
                .font(.headline)
                .lineLimit(2)
            HStack(spacing: LayoutMetrics.compact / 2) {
                if let version = mod.version, !version.isEmpty {
                    Text(verbatim: version)
                }
                if !mod.cachedUsername.isEmpty {
                    Text(verbatim: localization.string("%@ 작성", mod.cachedUsername))
                }
            }
            .font(.caption)
            .foregroundStyle(.secondary)
            .lineLimit(1)
        }
        .padding(.vertical, LayoutMetrics.compact / 2)
    }
}

private struct ModCatalogDetail: View {
    @EnvironmentObject private var model: ModCatalogViewModel
    @EnvironmentObject private var localization: LocalizationController
    let mod: RemoteMod

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: LayoutMetrics.sectionSpacing) {
                remoteImage

                VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                    Text(verbatim: mod.name)
                        .font(.title.bold())
                        .textSelection(.enabled)
                    HStack(spacing: LayoutMetrics.compact) {
                        if let version = mod.version, !version.isEmpty {
                            Label(version, systemImage: "number")
                        }
                        if !mod.cachedUsername.isEmpty {
                            Label(mod.cachedUsername, systemImage: "person")
                        }
                        if mod.uploadedTimestamp > 0 {
                            Label {
                                Text(uploadedDate, style: .date)
                            } icon: {
                                Image(systemName: "calendar")
                            }
                        }
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                }

                primaryAction

                if !mod.description.isEmpty {
                    Divider()
                    DiscordMessageView(mod.description)
                }
            }
            .padding(LayoutMetrics.pageHorizontal)
            .frame(maxWidth: LayoutMetrics.readableContentWidth, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }

    @ViewBuilder
    private var remoteImage: some View {
        if let imageURL = mod.imageURL.flatMap(URL.init(string:)), imageURL.scheme == "https" {
            AsyncImage(url: imageURL) { phase in
                switch phase {
                case .empty:
                    ProgressView()
                        .frame(maxWidth: .infinity, minHeight: 180)
                case let .success(image):
                    image
                        .resizable()
                        .scaledToFit()
                        .frame(maxWidth: 520, maxHeight: 220, alignment: .leading)
                case .failure:
                    EmptyView()
                @unknown default:
                    EmptyView()
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius))
        }
    }

    @ViewBuilder
    private var primaryAction: some View {
        if model.downloadingID == mod.id {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                ProgressView()
                    .controlSize(.small)
                Text(verbatim: localization.string("모드 다운로드 중…"))
                Button(localization.string("취소"), role: .cancel) { model.cancelDownload() }
            }
        } else if mod.action == .openWebsite {
            VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                Button { model.performPrimaryAction(for: mod) } label: {
                    Label(localization.string("웹사이트에서 받기"), systemImage: "arrow.up.right.square")
                }
                .buttonStyle(.bordered)
                .disabled(model.downloadingID != nil || mod.preferredDownloadURL == nil)
                .help(localization.string("모드 다운로드 웹사이트 열기"))
                Text(verbatim: localization.string("기본 웹 브라우저에서 열립니다."))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
        } else {
            Button { model.performPrimaryAction(for: mod) } label: {
                Label(localization.string("다운로드 및 설치"), systemImage: "arrow.down.circle")
            }
            .buttonStyle(.borderedProminent)
            .disabled(model.downloadingID != nil || mod.preferredDownloadURL == nil)
        }
    }

    private var uploadedDate: Date {
        Date(timeIntervalSince1970: TimeInterval(mod.uploadedTimestamp) / 1_000)
    }
}
