import Foundation

protocol EngineServing: Sendable {
    func run(_ request: EngineRequest) async throws -> EngineResponse
}

protocol ModCatalogServing: Sendable {
    func fetchMods() async throws -> [RemoteMod]
    func download(_ mod: RemoteMod) async throws -> URL
}

protocol GameLocating {
    func locate() -> URL?
    func validate(_ url: URL) -> Bool
}

protocol LogReading: Sendable {
    func exists(at url: URL) -> Bool
    func tail(of url: URL, lineLimit: Int) throws -> String
}

@MainActor
protocol WorkspaceOpening {
    func open(_ url: URL)
    func reveal(_ url: URL)
}
