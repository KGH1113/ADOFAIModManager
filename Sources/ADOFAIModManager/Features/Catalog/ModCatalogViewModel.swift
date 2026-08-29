import Combine
import Foundation

@MainActor
final class ModCatalogViewModel: ObservableObject {
    @Published private(set) var mods: [RemoteMod] = []
    @Published var searchText = ""
    @Published var selectedID: RemoteMod.ID?
    @Published private(set) var isLoading = false
    @Published private(set) var loadErrorKey: String?
    @Published private(set) var downloadingID: RemoteMod.ID?
    @Published var websiteFallback: RemoteMod?
    @Published var downloadErrorKey: String?

    private let app: AppModel
    private let service: any ModCatalogServing
    private var downloadTask: Task<Void, Never>?
    private var didLoad = false

    init(app: AppModel, service: any ModCatalogServing) {
        self.app = app
        self.service = service
    }

    var filteredMods: [RemoteMod] {
        let query = searchText.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !query.isEmpty else { return mods }
        return mods.filter { $0.matches(searchQuery: query) }
    }

    var selectedMod: RemoteMod? {
        guard let selectedID else { return nil }
        return filteredMods.first { $0.id == selectedID }
    }

    func selectFirstVisibleIfNeeded() {
        let visible = filteredMods
        guard !visible.contains(where: { $0.id == selectedID }) else { return }
        selectedID = visible.first?.id
    }

    func loadIfNeeded() async {
        guard !didLoad else { return }
        await refresh()
    }

    func refresh() async {
        guard !isLoading else { return }
        isLoading = true
        loadErrorKey = nil
        defer { isLoading = false }
        do {
            let currentSelection = selectedID
            mods = try await service.fetchMods()
            didLoad = true
            selectedID = mods.contains { $0.id == currentSelection }
                ? currentSelection
                : mods.first?.id
        } catch is CancellationError {
            return
        } catch {
            if mods.isEmpty {
                loadErrorKey = "모드 목록을 불러오지 못했습니다."
            } else {
                downloadErrorKey = "모드 목록을 새로 고치지 못했습니다."
            }
        }
    }

    func performPrimaryAction(for mod: RemoteMod) {
        switch mod.action {
        case .openWebsite:
            openWebsite(for: mod)
        case .downloadAndInstall, .downloadAndInspect:
            startDownload(mod)
        }
    }

    func startDownload(_ mod: RemoteMod) {
        guard downloadingID == nil else { return }
        downloadingID = mod.id
        downloadErrorKey = nil
        downloadTask = Task { [weak self] in
            guard let self else { return }
            do {
                let url = try await service.download(mod)
                guard !Task.isCancelled else {
                    CatalogDownloadStorage.remove(url)
                    downloadingID = nil
                    downloadTask = nil
                    return
                }
                app.offerDownloadedModFile(url)
            } catch is CancellationError {
                // Cancellation is direct user feedback, not an error.
            } catch let error as URLError where error.code == .cancelled {
                // URLSession reports task cancellation as URLError.cancelled.
            } catch ModCatalogError.notZip {
                websiteFallback = mod
            } catch ModCatalogError.fileTooLarge {
                downloadErrorKey = "모드 파일이 너무 커서 다운로드할 수 없습니다."
            } catch {
                downloadErrorKey = "모드를 다운로드하지 못했습니다."
            }
            downloadingID = nil
            downloadTask = nil
        }
    }

    func cancelDownload() {
        downloadTask?.cancel()
    }

    func openWebsite(for mod: RemoteMod) {
        guard let url = mod.preferredDownloadURL else {
            downloadErrorKey = "모드 다운로드 주소가 올바르지 않습니다."
            return
        }
        websiteFallback = nil
        app.openWebsite(url)
    }
}
