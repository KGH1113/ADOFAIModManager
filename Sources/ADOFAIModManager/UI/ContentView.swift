import SwiftUI

struct ContentView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var localization: LocalizationController
    @State private var isDropTargeted = false

    var body: some View {
        NavigationSplitView {
            List(SidebarSection.allCases, selection: $model.selection) { section in
                Label(localization.string(section.titleKey), systemImage: section.symbol).tag(section)
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 210)
        } detail: {
            Group {
                switch model.selection ?? .install {
                case .install: InstallationView()
                case .mods: ModsView()
                case .logs: LogsView()
                }
            }
        }
        .dropDestination(for: URL.self) { urls, _ in
            guard let zip = urls.first(where: {
                $0.isFileURL && $0.pathExtension.caseInsensitiveCompare("zip") == .orderedSame
            }) else { return false }
            model.offerModFile(zip)
            return true
        } isTargeted: { isDropTargeted = $0 }
        .overlay {
            if isDropTargeted {
                ZStack {
                    Color.accentColor.opacity(0.10)
                    VStack(spacing: LayoutMetrics.rowSpacing) {
                        Image(systemName: "shippingbox.and.arrow.backward.fill").font(.system(size: 38))
                        Text(verbatim: localization.string("모드 ZIP을 여기에 놓으세요")).font(.title2.bold())
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
                    pending.inspection.alreadyInstalled ? "“%@” 모드를 교체하시겠습니까?" : "“%@” 모드를 설치하시겠습니까?",
                    pending.inspection.name
                )),
                message: Text(verbatim: pending.inspection.version.isEmpty
                    ? localization.string("확인하면 모드 폴더에 추가됩니다.")
                    : localization.string("버전 %@ · 확인하면 모드 폴더에 추가됩니다.", pending.inspection.version)),
                primaryButton: .default(Text(verbatim: localization.string(
                    pending.inspection.alreadyInstalled ? "교체" : "설치"
                ))) { Task { await model.confirmModInstall(pending) } },
                secondaryButton: .cancel(Text(verbatim: localization.string("취소"))) {
                    model.cancelModInstall()
                }
            )
        }
    }
}
