import Combine

@MainActor
final class ModsViewModel: ObservableObject {
    private let app: AppModel
    private var observation: AnyCancellable?

    init(app: AppModel) {
        self.app = app
        observation = app.objectWillChange.sink { [weak self] _ in self?.objectWillChange.send() }
    }

    var mods: [InstalledMod] { app.mods }
    var status: InstallStatus? { app.status }
    var isWorking: Bool { app.isWorking }
    var isInspectingMod: Bool { app.isInspectingMod }

    func refresh(includeMods: Bool = false) async { await app.refresh(includeMods: includeMods) }
    func openModsFolder() { app.openModsFolder() }
    func chooseMod() { app.chooseMod() }
    func setMod(_ mod: InstalledMod, enabled: Bool) async { await app.setMod(mod, enabled: enabled) }
    func removeMod(_ mod: InstalledMod) async { await app.removeMod(mod) }
}
