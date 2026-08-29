@MainActor
struct AppContainer {
    let engine: any EngineServing
    let modCatalog: any ModCatalogServing
    let gameLocator: any GameLocating
    let logReader: any LogReading
    let workspace: any WorkspaceOpening

    static func live() -> AppContainer {
        AppContainer(
            engine: ProcessEngineClient(),
            modCatalog: ModCatalogClient(),
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
