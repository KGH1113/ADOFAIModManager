import SwiftUI

@main
struct ADOFAIModManagerApp: App {
    @StateObject private var localization: LocalizationController
    @StateObject private var model: AppModel

    init() {
        let localization = LocalizationController()
        _localization = StateObject(wrappedValue: localization)
        _model = StateObject(wrappedValue: AppModel(localization: localization))
    }

    var body: some Scene {
        Window("ADOFAI Mod Manager", id: "main") {
            ContentView()
                .environmentObject(model)
                .environmentObject(localization)
                .environment(\.locale, localization.locale)
                .task { await model.start() }
                .onOpenURL { model.offerModFile($0) }
                .frame(minWidth: 820, minHeight: 560)
        }
        .defaultSize(width: 960, height: 650)
        .windowStyle(.hiddenTitleBar)

        Settings {
            SettingsView()
                .environmentObject(localization)
                .environment(\.locale, localization.locale)
        }
    }
}

private struct SettingsView: View {
    @EnvironmentObject private var localization: LocalizationController

    var body: some View {
        Form {
            Section(localization.string("언어")) {
                Picker(
                    localization.string("앱 언어"),
                    selection: Binding(
                        get: { localization.language },
                        set: { localization.select($0) }
                    )
                ) {
                    ForEach(AppLanguage.allCases) { language in
                        Text(verbatim: localization.displayName(for: language))
                            .tag(language)
                    }
                }
                .pickerStyle(.menu)

                Text(verbatim: localization.string("변경 사항은 모든 앱 창에 바로 적용됩니다."))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Section {
                HStack(alignment: .top, spacing: LayoutMetrics.rowSpacing) {
                    Image(systemName: "puzzlepiece.extension")
                        .font(.system(size: 32))
                    VStack(alignment: .leading, spacing: LayoutMetrics.compact) {
                        Text(verbatim: "ADOFAI Mod Manager")
                            .font(.title2.bold())
                        Text(verbatim: localization.string(
                            "Unity Mod Manager 및 7th Beat Games의 공식 제품이 아닌 커뮤니티 도구입니다."
                        ))
                        .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .formStyle(.grouped)
        .padding(LayoutMetrics.pageVertical)
        .frame(width: 460)
    }
}
