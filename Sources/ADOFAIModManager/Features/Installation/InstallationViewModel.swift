import Combine
import Foundation

@MainActor
final class InstallationViewModel: ObservableObject {
    private let app: AppModel
    private var observation: AnyCancellable?

    init(app: AppModel) {
        self.app = app
        observation = app.objectWillChange.sink { [weak self] _ in self?.objectWillChange.send() }
    }

    var status: InstallStatus? { app.status }
    var isInstalled: Bool { app.isInstalled }
    var gameURL: URL? { app.gameURL }
    var gameLocationDescriptionKey: String { app.gameLocationDescriptionKey }
    var isWorking: Bool { app.isWorking }
    var activityKey: String? { app.activityKey }

    func refresh(includeMods: Bool = false) async { await app.refresh(includeMods: includeMods) }
    func chooseGame() { app.chooseGame() }
    func install(repair: Bool = false) async { await app.install(repair: repair) }
    func removeHook() async { await app.removeHook() }
    func restoreOriginal() async { await app.restoreOriginal() }
}
