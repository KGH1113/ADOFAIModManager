import SwiftUI

struct LogsView: View {
    @EnvironmentObject private var model: LogsViewModel
    @EnvironmentObject private var localization: LocalizationController
    @State private var source = LogSource.defaultSource

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: LayoutMetrics.sectionSpacing) {
                PageTitle(title: localization.string("로그"),
                          subtitle: localization.string(source == .game ? "게임 실행 기록" : "UMM 작업 기록"))
                Spacer()
                Picker(localization.string("로그 종류"), selection: $source) {
                    ForEach(LogSource.allCases) { item in Text(verbatim: localization.string(item.titleKey)).tag(item) }
                }
                .pickerStyle(.segmented).labelsHidden().frame(width: 240)
            }
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.top, LayoutMetrics.pageVertical)
            .padding(.bottom, LayoutMetrics.cardPadding)
            Divider()
            switch source {
            case .game: GameLogView()
            case .umm: UMMLogView()
            }
        }
    }
}

private struct GameLogView: View {
    @EnvironmentObject private var model: LogsViewModel
    @EnvironmentObject private var localization: LocalizationController
    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.rowSpacing) {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                Label(localization.string("최근 30줄"), systemImage: "gamecontroller").font(.headline)
                Spacer()
                Button { Task { await model.refreshGameLog() } } label: {
                    if model.isRefreshingGameLog { ProgressView().controlSize(.small) }
                    else { Label(localization.string("새로 고침"), systemImage: "arrow.clockwise") }
                }.disabled(model.isRefreshingGameLog).help(localization.string("게임 로그 새로 고침"))
                Button { model.openGameLog() } label: {
                    Label(localization.string("Finder에서 보기"), systemImage: "folder")
                }.disabled(!model.gameLogExists)
            }
            LogSurface {
                LogTextOrEmpty(text: model.gameLogText,
                               title: localization.string("아직 표시할 게임 로그가 없습니다"),
                               description: localization.string("게임을 한 번 실행하면 로그가 생성됩니다."))
            }
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal).padding(.vertical, LayoutMetrics.cardPadding)
        .task { await model.refreshGameLog() }
    }
}

private struct UMMLogView: View {
    @EnvironmentObject private var model: LogsViewModel
    @EnvironmentObject private var localization: LocalizationController
    var body: some View {
        VStack(alignment: .leading, spacing: LayoutMetrics.rowSpacing) {
            HStack(spacing: LayoutMetrics.rowSpacing) {
                Label(localization.string("UMM 로그"), systemImage: "wrench.and.screwdriver").font(.headline)
                Spacer()
                Button { Task { await model.refreshUMMLog() } } label: {
                    if model.isRefreshingUMMLog { ProgressView().controlSize(.small) }
                    else { Label(localization.string("새로 고침"), systemImage: "arrow.clockwise") }
                }.disabled(model.isRefreshingUMMLog || model.gameURL == nil)
                Button { model.openUMMLog() } label: {
                    Label(localization.string("Finder에서 보기"), systemImage: "folder")
                }.disabled(!model.ummLogExists)
            }
            LogSurface {
                LogTextOrEmpty(text: model.ummLogText,
                               title: localization.string("아직 UMM 로그가 없습니다"),
                               description: localization.string("게임에서 UMM이 실행되면 로그가 생성됩니다."))
            }
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal).padding(.vertical, LayoutMetrics.cardPadding)
        .task { await model.refreshUMMLog() }
    }
}

private struct LogTextOrEmpty: View {
    let text: String
    let title: String
    let description: String
    var body: some View {
        if text.isEmpty {
            ContentUnavailableView(title, systemImage: "text.page", description: Text(verbatim: description))
        } else {
            ScrollView([.horizontal, .vertical]) {
                Text(verbatim: text).font(.system(.body, design: .monospaced)).textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading).padding(LayoutMetrics.cardPadding)
            }
        }
    }
}

private struct LogSurface<Content: View>: View {
    @ViewBuilder let content: Content
    var body: some View {
        content.frame(maxWidth: .infinity, minHeight: 340, maxHeight: .infinity)
            .background(Color(nsColor: .textBackgroundColor), in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius))
            .overlay { RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius).stroke(.separator.opacity(0.55), lineWidth: 1) }
    }
}
