import CoreGraphics
import Foundation

enum LayoutMetrics {
    static let unit: CGFloat = 8
    static let compact: CGFloat = 8
    static let rowSpacing: CGFloat = 12
    static let sectionSpacing: CGFloat = 24
    static let pageHorizontal: CGFloat = 24
    static let pageVertical: CGFloat = 28
    static let cardPadding: CGFloat = 20
    static let cardCornerRadius: CGFloat = 16
    static let readableContentWidth: CGFloat = 760
    static let sidebarWidth: CGFloat = 210
    static let catalogListWidth: CGFloat = 260
}

enum GameLogConfiguration {
    static let lineLimit = 30

    static var playerLogURL: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appending(path: "Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log")
    }
}
