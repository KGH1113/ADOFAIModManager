@MainActor
struct AppContainer {
    let engine: any EngineServing
    let gameLocator: any GameLocating
    let logReader: any LogReading
    let workspace: any WorkspaceOpening

    static func live() -> AppContainer {
        AppContainer(
            engine: ProcessEngineClient(),
            gameLocator: MacSteamGameLocator(),
            logReader: FileLogReader(),
            workspace: MacWorkspaceOpener()
        )
    }

    func makeAppModel(localization: LocalizationController) -> AppModel {
        AppModel(
            localization: localization,
            engine: engine,
            gameLocator: gameLocator,
            logReader: logReader,
            workspace: workspace
        )
    }
}
