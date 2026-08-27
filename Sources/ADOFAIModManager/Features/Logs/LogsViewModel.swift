import Combine
import Foundation

@MainActor
final class LogsViewModel: ObservableObject {
    private let app: AppModel
    private var observation: AnyCancellable?

    init(app: AppModel) {
        self.app = app
        observation = app.objectWillChange.sink { [weak self] _ in self?.objectWillChange.send() }
    }

    var gameURL: URL? { app.gameURL }
    var gameLogText: String { app.gameLogText }
    var gameLogExists: Bool { app.gameLogExists }
    var isRefreshingGameLog: Bool { app.isRefreshingGameLog }
    var ummLogText: String { app.ummLogText }
    var ummLogExists: Bool { app.ummLogExists }
    var isRefreshingUMMLog: Bool { app.isRefreshingUMMLog }

    func refreshGameLog() async { await app.refreshGameLog() }
    func openGameLog() { app.openGameLog() }
    func refreshUMMLog() async { await app.refreshUMMLog() }
    func openUMMLog() { app.openUMMLog() }
}
