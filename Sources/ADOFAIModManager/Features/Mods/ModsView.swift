import SwiftUI

struct ModsView: View {
    @EnvironmentObject private var model: ModsViewModel
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        VStack(spacing: 0) {
            HStack(alignment: .center, spacing: LayoutMetrics.rowSpacing) {
                PageTitle(title: localization.string("모드"), subtitle: localization.installedModCount(model.mods.count))
                Spacer()
                Menu {
                    Button { Task { await model.refresh(includeMods: true) } } label: {
                        Label(localization.string("목록 새로 고침"), systemImage: "arrow.clockwise")
                    }.disabled(model.isWorking)
                    Button { model.openModsFolder() } label: {
                        Label(localization.string("모드 폴더 열기"), systemImage: "folder")
                    }.disabled(model.status?.modsPath == nil)
                } label: { Label(localization.string("목록 관리"), systemImage: "ellipsis.circle") }
                .menuStyle(.borderlessButton).fixedSize()
                Button { model.chooseMod() } label: {
                    if model.isInspectingMod {
                        HStack(spacing: LayoutMetrics.compact) {
                            ProgressView().controlSize(.small)
                            Text(verbatim: localization.string("모드 확인 중…"))
                        }
                    } else { Label(localization.string("모드 추가"), systemImage: "plus") }
                }
                .buttonStyle(.borderedProminent).disabled(model.isInspectingMod || model.isWorking)
            }
            .padding(.horizontal, LayoutMetrics.pageHorizontal)
            .padding(.top, LayoutMetrics.pageVertical)
            .padding(.bottom, LayoutMetrics.cardPadding)
            Divider()
            if model.mods.isEmpty {
                ContentUnavailableView(
                    localization.string("설치된 모드가 없습니다"),
                    systemImage: "puzzlepiece.extension",
                    description: Text(verbatim: localization.string("모드 추가 버튼으로 UMM 모드를 추가할 수 있습니다."))
                ).frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                ScrollView {
                    LazyVStack(spacing: 0) {
                        ForEach(model.mods) { mod in
                            ModRow(mod: mod)
                            if mod.id != model.mods.last?.id {
                                Divider().padding(.leading, LayoutMetrics.pageHorizontal + 32)
                            }
                        }
                    }
                }
            }
        }
    }
}

private struct ModRow: View {
    @EnvironmentObject private var model: ModsViewModel
    @EnvironmentObject private var localization: LocalizationController
    let mod: InstalledMod

    var body: some View {
        HStack(spacing: LayoutMetrics.rowSpacing) {
            Toggle(localization.string(mod.enabled ? "모드 켜짐" : "모드 꺼짐"), isOn: Binding(
                get: { mod.enabled },
                set: { newValue in Task { await model.setMod(mod, enabled: newValue) } }
            ))
            .toggleStyle(.checkbox).labelsHidden()
            .help(localization.string(mod.enabled ? "모드 끄기" : "모드 켜기"))
            .disabled(!mod.installed || model.isWorking)
            VStack(alignment: .leading, spacing: LayoutMetrics.compact / 2) {
                Text(mod.name).font(.headline)
                Text(verbatim: "\(mod.id) · \(mod.version)").font(.caption).foregroundStyle(.secondary)
            }
            Spacer(minLength: LayoutMetrics.sectionSpacing)
            Button(role: .destructive) { Task { await model.removeMod(mod) } } label: {
                Label(localization.string("모드 제거"), systemImage: "trash").labelStyle(.iconOnly)
            }
            .buttonStyle(.borderless).help(localization.string("모드 제거")).disabled(model.isWorking)
        }
        .padding(.horizontal, LayoutMetrics.pageHorizontal)
        .padding(.vertical, LayoutMetrics.rowSpacing)
    }
}
