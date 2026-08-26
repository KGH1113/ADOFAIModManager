import Foundation
import SwiftUI

enum AppLanguage: String, CaseIterable, Identifiable {
    case system
    case korean = "ko"
    case english = "en"
    case simplifiedChinese = "zh-Hans"

    static let storageKey = "appLanguage"

    var id: String { rawValue }

    var locale: Locale {
        switch self {
        case .system:
            .autoupdatingCurrent
        case .korean:
            Locale(identifier: "ko")
        case .english:
            Locale(identifier: "en")
        case .simplifiedChinese:
            Locale(identifier: "zh-Hans")
        }
    }

    var nativeName: String? {
        switch self {
        case .system: nil
        case .korean: "한국어"
        case .english: "English"
        case .simplifiedChinese: "简体中文"
        }
    }

}

@MainActor
final class LocalizationController: ObservableObject {
    @Published private(set) var language: AppLanguage

    private let defaults: UserDefaults
    private let resourceBundle: Bundle

    init(
        defaults: UserDefaults = .standard,
        resourceBundle: Bundle = .module
    ) {
        self.defaults = defaults
        self.resourceBundle = resourceBundle
        language = defaults.string(forKey: AppLanguage.storageKey)
            .flatMap(AppLanguage.init(rawValue:)) ?? .system
    }

    var locale: Locale { language.locale }

    func select(_ language: AppLanguage) {
        guard self.language != language else { return }
        self.language = language
        defaults.set(language.rawValue, forKey: AppLanguage.storageKey)
    }

    func string(_ key: String, _ arguments: CVarArg...) -> String {
        let format = activeBundle.localizedString(
            forKey: key,
            value: key,
            table: "Localizable"
        )
        guard !arguments.isEmpty else { return format }
        return String(format: format, locale: locale, arguments: arguments)
    }

    func installedModCount(_ count: Int) -> String {
        let key = count == 1 ? "mods.installed_count.one" : "mods.installed_count.other"
        return string(key, Int64(count))
    }

    func displayName(for language: AppLanguage) -> String {
        language.nativeName ?? string("시스템 설정 따르기")
    }

    private var activeBundle: Bundle {
        guard language != .system else { return resourceBundle }
        let resourceName = resourceBundle.localizations.first {
            $0.caseInsensitiveCompare(language.rawValue) == .orderedSame
        } ?? language.rawValue
        guard
              let path = resourceBundle.path(forResource: resourceName, ofType: "lproj"),
              let bundle = Bundle(path: path) else {
            return resourceBundle
        }
        return bundle
    }
}
