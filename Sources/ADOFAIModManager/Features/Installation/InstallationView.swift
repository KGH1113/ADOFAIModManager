import SwiftUI

struct InstallationView: View {
    @EnvironmentObject private var model: InstallationViewModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: LayoutMetrics.sectionSpacing) {
                PageTitle(title: "ADOFAI Mod Manager", subtitle: localization.string("얼불춤에 UMM을 설치하고 모드를 관리합니다."))
                statusCard
                actions
                if model.status?.warning != nil {
                    Label {
                        Text(verbatim: localization.string("설치 상태를 확인해야 합니다. ‘설치 문제 해결’을 실행해 주세요."))
                    } icon: { Image(systemName: "exclamationmark.triangle.fill") }
                    .foregroundStyle(.orange)
                    .padding(LayoutMetrics.cardPadding)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .background(.orange.opacity(0.09), in: RoundedRectangle(cornerRadius: LayoutMetrics.cardCornerRadius))
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
                    Text(verbatim: localization.string("설치 상태")).font(.headline)
                } icon: {
                    Image(systemName: model.isInstalled ? "checkmark.seal.fill" : "circle.dashed")
                        .foregroundStyle(model.isInstalled ? .green : .secondary)
                }
                Spacer()
                Button { Task { await model.refresh(includeMods: true) } } label: { Image(systemName: "arrow.clockwise") }
                    .buttonStyle(.borderless).disabled(model.isWorking)
            }
            Divider()
            HStack(alignment: .center, spacing: LayoutMetrics.rowSpacing) {
                VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                    Text(verbatim: localization.string("얼불춤")).foregroundStyle(.secondary)
                    Text(verbatim: localization.string(model.gameURL == nil ? "찾지 못함" : model.gameLocationDescriptionKey))
                        .foregroundStyle(model.gameURL == nil ? .secondary : .primary)
                }
                Spacer(minLength: LayoutMetrics.sectionSpacing)
                Button(action: model.chooseGame) {
                    Label(localization.string(model.gameURL == nil ? "게임 선택" : "게임 폴더 다시 선택"), systemImage: "folder")
                }
            }
            VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                Text(verbatim: "UMM").foregroundStyle(.secondary)
                Text(verbatim: model.status?.managerVersion.map { localization.string("설치됨 · 버전 %@", $0) }
                    ?? localization.string("설치 필요"))
                    .foregroundStyle(model.status?.managerVersion == nil ? .secondary : .primary)
            }.frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(LayoutMetrics.cardPadding)
        .adaptiveGlass()
    }

    private var actions: some View {
        HStack(spacing: LayoutMetrics.rowSpacing) {
            Button { Task { await model.install(repair: model.isInstalled) } } label: {
                HStack(spacing: LayoutMetrics.compact) {
                    if model.isWorking { ProgressView().controlSize(.small).accessibilityHidden(true) }
                    else { Image(systemName: model.isInstalled ? "arrow.clockwise" : "arrow.down.app") }
                    Text(verbatim: primaryActionTitle)
                }
            }
            .buttonStyle(.borderedProminent).controlSize(.large)
            .disabled(model.gameURL == nil || model.isWorking)
            Menu {
                Button { Task { await model.install(repair: true) } } label: {
                    Label(localization.string("설치 문제 해결"), systemImage: "wrench.and.screwdriver")
                }
                Divider()
                Button(role: .destructive) { Task { await model.removeHook() } } label: {
                    Label(localization.string("UMM 제거"), systemImage: "trash")
                }.disabled(model.status?.hookInstalled != true)
                Button { Task { await model.restoreOriginal() } } label: {
                    Label(localization.string("설치 전 상태로 되돌리기"), systemImage: "arrow.uturn.backward")
                }.disabled(model.status?.hasBackup != true)
            } label: { Label(localization.string("더 보기"), systemImage: "ellipsis.circle") }
            .menuStyle(.borderlessButton).fixedSize().disabled(model.gameURL == nil || model.isWorking)
            Spacer(minLength: 0)
        }
    }

    private var primaryActionTitle: String {
        if model.isWorking { return model.activityKey.map { localization.string($0) } ?? "" }
        return localization.string(model.isInstalled ? "다시 설치" : "UMM 설치")
    }
}
